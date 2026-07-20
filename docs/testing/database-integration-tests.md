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

`RUN_INTEGRATION_TESTS=true` 启用全部外部 Provider。连接字符串应通过 CI 密钥或本地 `.runsettings` 注入，日志、异常和测试输出不得回显密码。

## 验收重点

- Scope 创建的 Query 和 Executor 必须共享同一连接、事务和固定数据库上下文；
- 外部连接或事务身份不一致必须在执行前拒绝，且 Query 不提交、回滚或释放外部资源；
- 提交失败必须尝试回滚，两个失败原因均应保留；
- 每个 Provider 的 Provider、Query 和 Executor 注册均必须能够通过 `ISqlDbConnectionFactoryResolver` 创建对应独立连接；
- EF Shared 只复用 DbContext 的连接与当前事务，EF Independent 使用框架自有连接。