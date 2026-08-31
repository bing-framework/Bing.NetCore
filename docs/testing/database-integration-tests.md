# SQL 事务与数据库集成测试

## 目标

本说明覆盖 SQL/Dapper 连接与事务 API 收敛后的本地验证方式。`ISqlTransactionScope` 是唯一推荐的事务生命周期入口；外部 Provider 测试只可连接受控测试库，禁止使用生产连接字符串。

## 无外部依赖验证

在仓库根目录运行：

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -nologo -v minimal
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -nologo -v minimal
dotnet test .\framework\tests\Bing.EntityFrameworkCore.Tests\Bing.EntityFrameworkCore.Tests.csproj -nologo -v minimal
```

SQLite 集成测试使用临时文件，不依赖网络、容器或数据库凭据。覆盖内容包括 Scope 提交/回滚、子 Query/Executor 租约、连接所有权、数据库上下文固定、EF Shared/Independent 和独立连接工厂。

## 外部 Provider 门控

MySQL、PostgreSQL、SQL Server 与 Oracle 集成测试默认跳过。仅在受保护 CI 或本机专用测试环境中设置对应变量后执行：

| Provider | 启用变量 | 连接字符串变量 |
| --- | --- | --- |
| MySQL | `RUN_MYSQL_INTEGRATION_TESTS=true` | `ConnectionStrings__MySqlConnection` |
| PostgreSQL | `RUN_POSTGRESQL_INTEGRATION_TESTS=true` | `ConnectionStrings__PostgreSqlConnection` |
| SQL Server | `RUN_SQLSERVER_INTEGRATION_TESTS=true` | `ConnectionStrings__SqlServerConnection` |
| Oracle | `RUN_ORACLE_INTEGRATION_TESTS=true` | `ConnectionStrings__OracleConnection` |

`RUN_INTEGRATION_TESTS=true` 仅用于本地同时验证多个外部 Provider；受保护 Provider CI 不得设置它。PostgreSQL 的唯一规范 gate 是 `RUN_POSTGRESQL_INTEGRATION_TESTS=true`，不支持 `RUN_PGSQL_INTEGRATION_TESTS`。连接字符串应通过 CI 密钥或用户显式选择的本地 runsettings 注入，日志、异常和测试输出不得回显密码。项目不会自动加载目录中的 `integration.runsettings`，避免普通构建继承本地连接或 gate。Provider CI 禁止 `ConnectionStrings__DefaultConnection` 回退。

## 受保护 Provider CI

每个 Provider lane 必须在 build 完成后使用 `--no-build` 调用同一个 runner，并仅注入自身的 Provider gate、专属连接字符串和 `ALLOW_DATABASE_RESET_FOR_TESTS=true`：

```powershell
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider PostgreSql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider SqlServer -Framework net8.0 -Configuration Release
```

AppVeyor 使用 `PROVIDER_TEST_LANE=mysql`、`postgresql` 或 `sqlserver` 选择对应调用路径；未设置时仅运行 `common` lane。Provider 专属变量和密钥只能在对应受保护的远端作业中配置；仓库不创建会在无密环境失败的 Provider matrix。在远端 secret scope、trusted-lane 策略和安全测试库未完成前，Provider job 不能作为已验收证据。

runner 在连接前验证规范 gate、专属连接字符串、reset 授权和安全测试数据库名，并为每个 Provider/TFM 写入独立 TRX/JSON 摘要。发现零测试、全部 Skip、core Provider Skip、全局 gate 或默认连接字符串时，runner 以非零退出。MySQL 跨库测试允许在未启用独立跨库配置时单独 Skip，不得掩盖 MySQL core 测试的执行结果。

## 验收重点

- Scope 创建的 Query 和 Executor 必须共享同一连接、事务和固定数据库上下文；
- 外部连接或事务身份不一致必须在执行前拒绝，且 Query 不提交、回滚或释放外部资源；
- 提交失败必须尝试回滚，两个失败原因均应保留；
- 每个 Provider 的 Provider、Query 和 Executor 注册均必须能够通过 `ISqlDbConnectionFactoryResolver` 创建对应独立连接；
- EF Shared 只复用 DbContext 的连接与当前事务，EF Independent 使用框架自有连接。
