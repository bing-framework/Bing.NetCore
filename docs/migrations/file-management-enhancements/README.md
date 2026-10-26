# 文件管理增强 schema 升级

这些脚本把既有基础文件管理 schema 升级到包含任务审计与恢复、批次迁移、额度预留、IO 租约和 Outbox 的版本。脚本与 ORM/数据库必须匹配；每个数据库只执行一份：

| 元数据实现 | SQLite | MySQL 8 |
| --- | --- | --- |
| EF Core | `efcore-sqlite.sql` | `efcore-mysql8.sql` |
| FreeSql | `freesql-sqlite.sql` | `freesql-mysql8.sql` |

脚本以 `ai_docs/tasks/20261024-file-storage-design/evidence/fix-round-1/schema-EFCore-sqlite.sql` 和 `schema-FreeSql-sqlite.sql` 所记录的基础表为起点。MySQL 脚本要求对应 ORM 的同一基础版本、MySQL 8 和 InnoDB；它们不负责从更早的旧文件模块或其他未知自定义 schema 迁移。先核实生产表、列和被替换的旧索引名与对应 SQLite 基线等价。

## 执行步骤

1. 停止所有 Admin、文件 Worker、维护 CLI 和任何写入文件管理表的进程，确认所有副本都已停止写入。新旧程序不能并行运行升级。
2. 对元数据库和文件内容存储做可恢复备份；需要一致回退时，两者必须来自同一停写窗口。
3. 先在生产结构副本执行对应脚本，核对 SQL、索引和预留记录数，再安排正式停写窗口。
4. 对每个数据库只执行 ORM 和数据库均匹配的一份脚本一次。脚本不是幂等迁移器；失败后先检查已执行语句和当前 schema，MySQL 脚本尤其不能直接从头重跑。
5. 核对后文的回填结果，再部署新版本并启动所有实例。配置中的租户与用户会话/字节限额默认 `0`，表示不施加新限额；仍会写入历史预留，以便将来启用限额时不漏算未结束的上传。

SQLite 脚本把 DDL 和回填放在一个 `BEGIN IMMEDIATE` 事务内。MySQL DDL 会隐式提交，整个脚本不能作为单一回滚事务；应使用已验证的备份和停写窗口。不要用新版本处理完一段新业务数据后，再通过删除新表或新列来回滚。

MySQL 新表继承目标数据库的字符集和排序规则，避免仅指定 `CHARSET=utf8mb4` 时意外采用服务器默认的 `utf8mb4_0900_ai_ci`，与旧表的 `utf8mb4_unicode_ci` 发生 JOIN 冲突。升级前应确认目标库与既有文件表的文本 GUID 列规则一致；自定义旧表需先在结构副本验证兼容性。已执行旧版脚本的库不能重跑整份升级，应在停写备份后单独校正受影响新表的字符集/排序规则，并检查索引及唯一键冲突。

## 数据回填

- `FileRecord.ContentPolicyJson` 设为 `{}`。旧文件没有历史策略，空策略不会伪造原始检查配置。
- `FileOperation.CreatedAt` 和 `FinishedAt` 设为 `0`。旧 schema 没保存这些时间，`0` 明确代表未知；不会从重试时间推断伪造创建时间。
- `FileUploadReservations` 从文件与上传会话重建：双方状态为 Pending、Uploading、Completing 或 Quarantined 时保留长度；Aborted/Failed 会话只有在没有任何成功的 Abort 操作时保留，直到清理确认。Ready 文件和已成功 Abort 清理的记录不预留。所有者取文件的 `CreatorId`，范围取 `ScopeKey`，时间取文件 `CreatedAt`。
- 新增任务审计、迁移批次与批次项、额度控制行、上传预留、IO 租约及 Outbox 七张表。运行中的批次/事件状态不会凭空创建。
- FreeSql 的 MySQL 映射使用原生 ENUM，与 SQLite 的整数枚举不同。MySQL 脚本为旧文件/会话追加 Quarantined、任务 Kind 追加 Inspect，新批次也使用原生 ENUM，回填以枚举名称匹配；不能把 SQLite 的整数条件直接搬到 MySQL。

升级后可执行以下核对：

```sql
SELECT COUNT(*) AS missing_policy
FROM BingFileRecords
WHERE ContentPolicyJson IS NULL OR ContentPolicyJson = '';

SELECT COUNT(*) AS missing_operation_times
FROM BingFileOperations
WHERE CreatedAt IS NULL OR FinishedAt IS NULL;

SELECT COUNT(*) AS reservations
FROM BingFileUploadReservations;

SELECT COALESCE(SUM(Bytes), 0) AS reserved_bytes
FROM BingFileUploadReservations;
```

前两项应为 `0`。预留行数和字节总数应与停写时的未结束会话清单相符；可按 `ScopeKey`、`OwnerId` 分组复核，不要在未确认 Abort 清理状态前手工删除预留。

## 回滚限制

应用升级后，新功能会向新增表写入审计、批次状态、租约、预留和生命周期事件。此时不能只删新表或新列，否则会丢失业务状态并可能破坏仍在运行的上传/清理。需要回滚时停止所有写入，先恢复与内容存储备份一致的数据库备份，再部署对应旧版本。若备份后接受了新版本写入，必须先设计并审核反向数据迁移；本目录不提供自动反向脚本。

SQLite 两份脚本由 `EnhancementSchemaUpgradeTests` 分别从保存的 EF/FreeSql 旧 SQLite DDL 建库、写入旧上传数据、执行升级 SQL，再使用当前 Store 验证旧记录、操作时间和额度预留。两份 MySQL 8 脚本已通过 `AdminFileEnhancementUpgradeTests.MySql8EnhancementUpgrade_BackfillsLegacyRowsAndSupportsNewOperations` 的隔离 MySQL 8.4 实际升级验证，包括新枚举、任务恢复和迁移条目。测试从当前 ORM 结构回退新增字段/表/索引及枚举至基础结构；正式上线仍需核对实际库的自定义结构和数据规模，并在其副本审查执行计划与锁等待。
