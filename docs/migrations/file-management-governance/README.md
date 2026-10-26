# 文件治理增量升级

起点是已经执行完 [文件增强迁移](../file-management-enhancements/README.md) 的 14 表版本。新增 `BingFileEventDeliveries`、`BingFileGovernanceAudits`、`BingFileHistoryArchives` 三表及索引，现有数据和对象不改变。不需要为历史已确认事件伪造消费者回执。

| ORM | SQLite | MySQL 8 |
|---|---|---|
| EF Core | [efcore-sqlite.sql](efcore-sqlite.sql) | [efcore-mysql8.sql](efcore-mysql8.sql) |
| FreeSQL | [freesql-sqlite.sql](freesql-sqlite.sql) | [freesql-mysql8.sql](freesql-mysql8.sql) |

先备份文件元数据库，停止全部旧版本 worker 与文件管理写入；确认目标库及 ORM 后，在变更窗口执行对应脚本一次，再部署所有新版服务。MySQL DDL 不是事务原子操作，失败时先核对已经创建的表/索引，不能盲目重跑。脚本不含 IF NOT EXISTS，避免掩盖错版本结构。MySQL 继承目标库字符集和排序规则。

新库使用 Admin `files schema` 输出当前完整结构；旧基础版先执行增强升级，再执行本目录治理升级。Demo 的 EnsureCreated 不会升级已有 EF 数据库，也必须执行对应升级脚本，或使用新的专用测试目录。

回滚应用前停止新版 worker。新增表含回执、幂等与审计信息，不得自动删除；恢复旧 dispatcher 会重新执行已有成功消费者，必须评估至少一次处理和保留的回执，不能混跑协议。自定义 IFileManagementStore 需实现新 Governance partial 契约并随业务模块一起重编译。
