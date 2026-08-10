# 阶段八：多数据库上下文收尾

## 完成状态

- 已完成：AsyncLocal 数据库上下文按 accessor 实例存储不可变快照；嵌套、异步子任务、`Task.WhenAll` 和不同容器之间不会通过可变 Holder 串用状态。
- 已完成：`IDatabaseScopeManager.Use(dbKey)` 保持原有 API，数据库作用域与 `IReadPreferenceScopeManager` 的读取偏好作用域可嵌套并恢复父上下文。
- 已完成：事务开始时固定主库数据源、映射配置、连接和事务对象；后续 Ambient Context 切换不会改变事务子 Query/Executor。
- 已完成：同一容器可通过不写默认数据源的 `Add*Provider` 方法注册 MySQL、PostgreSQL、SQL Server 和 SQLite，并通过具名 `AddSqlDataSource` 运行时路由。
- 已完成：SQLite 双文件测试覆盖数据库切换、事务固定、`buffered:false` 列表物化、提前终止流式读取和 `Task.WhenAll` 并行隔离。
- 已完成：EF Core Independent 模式经统一数据源解析器使用环境 `Use(dbKey)` 快照；Shared 模式继续验证物理数据库一致性。
- 已完成：Doris 保留 `DatabaseType.Doris` 兼容标识并路由至 MySQL Provider；兼容数据源默认设置 `IsReadOnly=true` 与 `SupportsTransactions=false`，也可使用 `DatabaseType.MySql` 配置独立 Mapping Profile 并显式设置这两个属性。只读数据源会拒绝框架可识别的结构化 Mutation、执行型存储过程和本地事务；原生 SQL 保持调用方显式负责的权限边界。
- 已完成：流式查询在提前终止、完成和异常路径正确释放 Reader；`buffered:false` 参数未删除或强制改写。

## 使用模式

```csharp
services.AddMySqlProvider();
services.AddPostgreSqlProvider();
services.AddSqlServerProvider();
services.AddSqliteProvider();

services.AddSqlDataSource("mysql", DatabaseType.MySql, mysqlConnectionString);
services.AddSqlDataSource("pgsql", DatabaseType.PgSql, postgreSqlConnectionString);
services.AddSqlDataSource("sqlserver", DatabaseType.SqlServer, sqlServerConnectionString);
services.AddSqlDataSource("doris", DatabaseType.MySql, dorisConnectionString,
    setupAction: source =>
    {
        source.MappingProfile = "doris";
        source.IsReadOnly = true;
        source.SupportsTransactions = false;
    });
```

旧的 `Add*Query` / `Add*Executor` 快捷注册仍适用于单默认数据源。它们不应用作同容器多 Provider 的能力注册入口。

## 测试结果（2026-07-16）

| 项目 | 总计 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: | ---: |
| `Bing.Data.Sql.Tests` | 944 | 944 | 0 | 0 |
| `Bing.Dapper.SqlServer.Tests` | 224 | 224 | 0 | 0 |
| `Bing.Dapper.Sqlite.Tests` | 14 | 14 | 0 | 0 |
| `Bing.Dapper.MySql.Tests` | 156 | 156 | 0 | 0 |
| `Bing.Dapper.PostgreSql.Tests` | 126 | 126 | 0 | 0 |
| `Bing.Dapper.Oracle.Tests` | 112 | 112 | 0 | 0 |
| `Bing.EntityFrameworkCore.Tests` | 10 | 10 | 0 | 0 |
| `Bing.Dapper.SqlServer.Tests.Integration` | 4 | 0 | 0 | 4 |

本轮新增测试：同容器四 Provider 路由、Doris MySQL 协议与事务限制、流式提前终止诊断、SQLite `buffered:false`、SQLite 流式资源释放、SQLite 并行双库，以及门控的外部三 Provider 切换。

## 外部集成测试

外部多 Provider 集成测试位于 `Bing.Dapper.SqlServer.Tests.Integration`，默认由 `IntegrationFact` 跳过。执行前需设置：

- `RUN_INTEGRATION_TESTS=true`
- `ConnectionStrings__DefaultConnection`（SQL Server）
- `ConnectionStrings__MySqlConnection`
- `ConnectionStrings__PostgreSqlConnection`

未配置这些外部数据库连接时，跳过不阻断本地或 CI 的无外部依赖测试。

## 剩余风险

- 中：Doris 能力边界由数据源配置声明，尚未接入真实 Doris 环境验证。建议在有可控 Doris CI 服务后增加只读查询、分页和能力拒绝集成测试。
- 低：`BeginAsync` 在 `netstandard2.0` 仍以同步方式开启底层事务；不影响事务数据源固定，但不提供真实异步打开语义。
- 非阻断：仓库保留 `net6.0` 生命周期、历史 NuGet 漏洞及 SQLite RID 相关警告，均不由本阶段引入。

## 结构化表引用收敛

- 已完成：实体映射缓存以实体、DbKey、Mapping Profile、Database、Schema、TableName 和表路由键隔离，避免不同映射复用错误对象名称。
- 已完成：类型化 `From` / `Join` 在最终 SQL 渲染阶段按 Provider 格式化 `Database`、`Schema`、`TableName` 和可选 `Alias`；旧字符串 API 继续保持原始片段语义。
- 已完成：对象名称模型不再承载连接路由、跨数据库链接或 SQLite 附加别名；这些场景由应用在原生 SQL 与连接所有权边界内显式处理。
- 已完成：MySQL/Doris、SQL Server、PostgreSQL、Oracle 和 SQLite 的对象名称限定规则集中在能力模型中，未知已声明 Provider 失败关闭。

## 2026-07-17 验收补充

- `Bing.Dapper.Sqlite.Tests.Integration` 使用临时 SQLite 文件执行真实 SQL；无需环境变量，`net6.0` 与 `net8.0` 共 66 项通过。
- SQL Server、PostgreSQL 与 MySQL 集成项目继续由 `IntegrationFact` 门控。未提供连接字符串时，它们成功构建并跳过；启用真实执行分别需要 `RUN_SQLSERVER_INTEGRATION_TESTS=true`、`RUN_POSTGRESQL_INTEGRATION_TESTS=true` 或 `RUN_MYSQL_INTEGRATION_TESTS=true`，以及相应的 `ConnectionStrings__*Connection`。
- 外部 Provider 的真实执行不应使用生产库。共享测试基建会要求明确启用开关，并校验测试数据库名称以避免清理过程误操作系统库或业务库。

## 2026-07-17 一致性补充

- EF Core SQL Query Factory 已统一使用 `显式 dbKey > Ambient dbKey > 默认 DatabaseContext > 默认数据源 > 唯一数据源` 的顺序。未知显式 key 不会回退；最终 descriptor 的 Provider 必须与 EF Core Provider 一致。
- EF Core Shared 模式以最终解析的数据源进行物理身份校验。SQLite 文件按绝对路径比较；普通 `:memory:` 为独占内存数据库并明确拒绝 Shared，命名 `file:` 共享内存 URI 才可比较。
- SQL Server、MySQL、PostgreSQL 身份解析要求同时具备服务器地址和数据库名称；Oracle 要求数据源。无法安全解析时拒绝 Shared，不输出完整连接字符串或凭据。
- `DatabaseScopeOptions.ReadPreference` 未指定时继承父级，显式 Default/Primary 覆盖父级；数据库 Scope 与读取偏好 Scope 均按严格 LIFO 恢复。`Current` 返回深快照，使用 `Update(...)` 才会写回。
- 事务 Scope 结束后，已创建的 Query/Executor 会因租约失效而拒绝继续执行；Before/After/Error 诊断事件发布互不共享的消息快照，并保留同一操作标识和最终 `DbKey`、读取偏好、事务标识。
- 本轮实际执行：`Bing.Data.Sql.Tests`（net6.0/net8.0，992 通过）、`Bing.EntityFrameworkCore.Tests`（net6.0，20 通过，含 SQLite 双文件真实执行）、`Bing.Dapper.SqlServer.Tests`（net6.0/net8.0，230 通过）。SQL Server net8.0 仍报告已有 SQLite RID `NETSDK1206` 警告。

## 多数据库边界加固补充

- Scope 释放改为执行流局部语义：栈顶正常恢复、栈内乱序抛错、当前流缺少帧时幂等返回。子流释放继承 Scope 不会阻止父流恢复，`SuppressFlow` 不会取得父流数据库上下文。
- 数据库身份解析改为贡献者模型。SQLite 构建器共享内存格式、默认端口、SQL Server `tcp:host,port` 和 Oracle EZConnect 均被规范；无法安全解析的 Oracle 别名或复杂描述符明确拒绝 EF Shared。
- 移除了可替换的 `IDatabaseContextSnapshotFactory`，Accessor、Scope、`SqlOptions`、Factory 与事务统一使用静态深快照机制。
- 事务 Scope 使用显式状态机；Lease 在结束前失效，清理异常不会中断后续资源释放，异步方法优先调用 Provider 的原生 ADO.NET 异步成员。
- 原生异步事务成员命中后不会再同步回退重复提交或回滚；开始事务失败时，Query 清理异常与原始失败会以聚合异常保留。
- 诊断消息增加 Mapping Profile；TenantId 默认不输出，需通过 `SqlOptions.IncludeTenantIdInDiagnostics` 显式启用。一维数组参数会按事件快照复制，SkyAPM 追加映射配置、读取偏好、隔离级别及条件租户标签。

### 本轮定向验证

| 项目 | 总计 | 通过 | 失败 | 备注 |
| --- | ---: | ---: | ---: | --- |
| `Bing.Data.Sql.Tests` | 1056 | 1056 | 0 | net6.0 / net8.0，含 Scope、完整描述符快照与身份解析 |
| `Bing.Dapper.SqlServer.Tests` | 252 | 252 | 0 | net6.0 / net8.0，含开始失败聚合与一维数组诊断快照 |
| `Bing.Dapper.Sqlite.Tests.Integration` | 76 | 76 | 0 | net6.0 / net8.0，含真实 SQLite 异常/取消 Scope 恢复 |
| `Bing.EntityFrameworkCore.Tests` | 23 | 23 | 0 | net6.0，含自定义身份贡献器的 Shared 比较 |

剩余风险：Oracle TNS 别名和复杂描述符刻意不做连接探测或别名展开，因此会拒绝 Shared；严格单地址 TCP TNS 可比较，但同时指定 Service Name 与 SID 的普通主机或 EZConnect 仍会拒绝 Shared。需要该能力的应用应使用 Independent 模式，或提供唯一的可比较目标。外部 Provider 集成测试仍由门控变量和受保护 CI 环境负责，且不使用生产连接。

## 2026-07-20 连接 API 收敛补充

- Dapper 默认连接创建统一为 `DatabaseType -> ISqlDbConnectionFactoryResolver -> IDbConnection`。五个 Provider 的 Provider、Query 与 Executor 注册均提供对应连接工厂。
- `IDatabaseConnectionAccessor` 承担跨 ORM 的只读连接访问；`IDatabase` 保持兼容继承关系，不并入事务 Scope。
- `IDatabaseFactory`、五个 `XxxDatabaseFactory` 及 Provider 基类的 `CreateDatabaseFactory` 已在 7.0.0 删除。Dapper 仅通过 `ISqlDbConnectionFactoryResolver` 创建自有连接。
- 外部连接身份校验覆盖内联及命名连接字符串；校验在释放旧 Owned 连接之前完成，拒绝绑定不会破坏原 Query 状态。
- EF Core Shared 每次执行前刷新当前外部事务，事务完成后不会缓存失效对象；Independent 继续使用自有连接。
