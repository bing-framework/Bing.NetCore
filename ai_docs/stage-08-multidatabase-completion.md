# 阶段八：多数据库上下文收尾

## 完成状态

- 已完成：AsyncLocal 数据库上下文按 accessor 实例存储不可变快照；嵌套、异步子任务、`Task.WhenAll` 和不同容器之间不会通过可变 Holder 串用状态。
- 已完成：`IDatabaseScopeManager.Use(dbKey)` 保持原有 API，数据库作用域与 `IReadPreferenceScopeManager` 的读取偏好作用域可嵌套并恢复父上下文。
- 已完成：事务开始时固定主库数据源、映射配置、连接和事务对象；后续 Ambient Context 切换不会改变事务子 Query/Executor。
- 已完成：同一容器可通过不写默认数据源的 `Add*Provider` 方法注册 MySQL、PostgreSQL、SQL Server 和 SQLite，并通过具名 `AddSqlDataSource` 运行时路由。
- 已完成：SQLite 双文件测试覆盖数据库切换、事务固定、`buffered:false` 列表物化、提前终止流式读取和 `Task.WhenAll` 并行隔离。
- 已完成：EF Core Independent 模式经统一数据源解析器使用环境 `Use(dbKey)` 快照；Shared 模式继续验证物理数据库一致性。
- 已完成：Doris 作为 MySQL 协议兼容数据源使用 `DatabaseType.MySql`，建议设置独立 Mapping Profile 与 `SupportsTransactions=false`；未新增 `DatabaseType.Doris`。
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