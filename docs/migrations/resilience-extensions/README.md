# Resilience Journal 生产迁移

这些脚本与 `RelationalOperationStore.InitializeAsync` 的 v1 结构保持一致，供宿主的正式迁移工具执行。

执行前将 `{{prefix}}` 替换为 `RelationalOperationStore` 使用的 `tablePrefix`，并把迁移版本登记到宿主的 schema history 表。生产启动不应调用 `InitializeAsync`；应用启动可调用 `ValidateSchemaAsync()` 只读校验结构；迁移版本历史由宿主检查。

- `sqlite/001-journal.sql`：SQLite 单机或本地部署。
- `mysql/001-journal.sql`：MySQL 多 Worker 部署，使用 `SKIP LOCKED` 所需的索引和字符集。

升级规则：后续结构变更必须追加编号脚本，先扩展后切换，保留旧处理器读取旧 Journal 的能力；禁止直接删除 `operations`、`attempts`、`audit` 中仍可能被 Worker 读取的列。

## 获取、验证与回滚

`store.GetMigrationScript()` 从包内嵌入资源返回当前方言、已替换安全前缀的 v1 SQL；`RelationalOperationStore.SchemaVersion` 为 1。脚本不自动执行。MySQL 使用 InnoDB、utf8mb4_bin、完整幂等唯一键和认领索引，DDL 不具备整份脚本的事务原子性；宿主迁移工具必须记录执行历史并在失败时检查已有结构再恢复，不能盲目重复 CREATE INDEX。

```csharp
var migration = store.GetMigrationScript(); // 发布流程审阅后执行
await store.ValidateSchemaAsync(stoppingToken); // SELECT/PRAGMA，不会执行 DDL
```

已使用 `InitializeAsync` 的现有 v1 库无须清空或重复建表：先校验，再由宿主登记接管版本。校验覆盖列类型/空值、主键、完整唯一键、认领索引，以及 MySQL 的事务引擎/二进制字符集。SQLite 列和关键索引必须保持 BINARY 排序规则（默认即为 BINARY），不能改为 NOCASE，以免操作 ID、状态或租约比较改变语义；校验不替代迁移历史、备份或业务表约束。

应用回滚保留 Journal、attempts、audit 和历史处理器；没有提供破坏数据的自动 down 脚本。本文不授权执行生产 DDL。SQLite 脚本、MySQL 实库、EF/FreeSQL/Dapper 同事务提交/回滚均由本任务 `RelationalBoundaryTests` 验证。
