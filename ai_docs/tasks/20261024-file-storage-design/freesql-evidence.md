# FreeSql 仓储验证证据

## 实现范围

- `components/src/Bing.Biz.FileManagement.FreeSql`：net6.0 FreeSql 3.5.311 仓储与表映射。
- `components/tests/Bing.Biz.FileManagement.FreeSql.Tests`：SQLite 实际数据库测试，目标 net6.0、net8.0。
- `modules/admin/src/Bing.Admin.FreeSQL/Modules/FileStorageModule.FreeSql.cs`：FreeSQL Admin 元数据装配。

`FreeSqlFileManagementStore` 完整实现 `IFileManagementStore`。每次调用创建自己的 FreeSql UnitOfWork 和短事务，所有命令绑定该事务；文件 CAS、会话版本、关联、分片和操作任务在同一事务内提交。任务租约通过版本、状态和到期条件原子更新，续租与完成另开短事务。唯一键错误按 SQLite、MySQL、PostgreSQL、SQL Server、Oracle Provider 错误码分类；仅文件 ID 或 RequestKey 已存在时 `TryCreateAsync` 返回 `false`，其他唯一键冲突继续抛出。

实体使用显式 Fluent 映射和索引：文件请求键唯一；会话按文件唯一；分片使用会话和序号联合主键；业务绑定和旧记录导入使用业务唯一索引；任务包含到期、租约和文件查询索引。表名与 EF Core 适配一致。

## 注册与 Schema

通用注册入口为 `AddBingFileManagementFreeSql(IServiceCollection, Func<IServiceProvider, IFreeSql>)`。工厂结果缓存为 singleton，仓储仅借用该实例，不会释放它；负责创建 `IFreeSql` 的宿主负责其生命周期。注册不会创建或同步表结构。

Admin 从 `ConnectionStrings:FileManagement` 读取独立元数据库连接，缺失时回退到 `ConnectionStrings:DefaultConnection`。它创建 DI 拥有的 singleton `IFreeSql`，并配置 `UseAutoSyncStructure(false)`。现有 Admin FreeSQL 业务 UOW 仍通过 `FreeSqlWrapper` 工作，未注册通用 `IFreeSql`，因此元数据连接不会覆盖业务 UOW。

测试或受控部署可显式调用 `FileManagementFreeSqlSchema.SyncStructure(orm)`。生产部署应使用审核后的数据库迁移；仓储注册和宿主启动不会调用此 API。

## 测试证据

运行命令：

```powershell
dotnet test components/tests/Bing.Biz.FileManagement.FreeSql.Tests/Bing.Biz.FileManagement.FreeSql.Tests.csproj -f net6.0
dotnet test components/tests/Bing.Biz.FileManagement.FreeSql.Tests/Bing.Biz.FileManagement.FreeSql.Tests.csproj -f net8.0 --no-restore
dotnet build modules/admin/src/Bing.Admin.FreeSQL/Bing.Admin.FreeSQL.csproj --no-restore -v:minimal
```

结果：net6.0 SQLite 7 通过、0 失败；net8.0 SQLite 7 通过、0 失败；FreeSQL Admin net6.0 构建成功，输出 55 个现存框架/依赖/文档警告，0 错误。测试对临时 SQLite 文件执行真实 DDL、插入、查询、更新和事务；没有使用 Mock 或业务数据库。

## 生产符号到测试方法

| 生产符号 / 行为 | 测试方法 |
| --- | --- |
| `FileManagementFreeSqlSchema.SyncStructure`、注册惰性缓存、借用生命周期且不自动建表 | `Registration_BorrowsClientAndDoesNotSynchronizeSchema` |
| `TryCreateAsync`、`FindAsync`、`FindSessionAsync`、`FindRequestAsync`、RequestKey 冲突 | `CreateAndFind_AreAtomicAndRequestKeyIsIdempotent` |
| 根版本 CAS、会话版本同步、关联替换、分片 upsert、过期 CAS 不写子记录 | `Update_CasReplacesBindingsUpsertsPartsAndRollsBackStaleChildren` |
| 范围/创建者过滤、分页、到期会话、可重试任务筛选 | `Lists_FilterScopeOwnerExpiryAndRetryability` |
| 任务 Claim / Renew / Finish、重试次数、版本和旧令牌隔离 | `LeaseClaimRenewFinish_UsesVersionAndCurrentToken` |
| 导入 upsert 及 Imported 状态保护 | `ImportUpsert_DoesNotRewriteImportedRecord` |
| 仓储提交不受之后无关 UOW 回滚影响 | `StoreTransaction_CommitsBeforeUnrelatedUnitOfWorkRollsBack` |

## 未覆盖项

- 未连接 Admin 或生产 MySQL；Admin 验证仅为编译，数据库 IO 验证使用隔离 SQLite 文件。
- 未验证其他 FreeSql 数据 Provider 的实际错误码和 DDL 行为。
- 未进行并发高负载或数据库故障注入测试；常规 CAS 与租约竞争由 SQLite 契约测试覆盖。
