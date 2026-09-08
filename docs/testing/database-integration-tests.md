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

`RUN_INTEGRATION_TESTS=true` 仅用于本地同时验证多个外部 Provider；受保护 Provider CI 不得设置它。PostgreSQL 的唯一规范 gate 是 `RUN_POSTGRESQL_INTEGRATION_TESTS=true`，不支持 `RUN_PGSQL_INTEGRATION_TESTS`。连接字符串应通过 CI 密钥、环境变量或未跟踪的 `integration.runsettings.local` 注入，日志、异常和测试输出不得回显密码。受保护 Provider runner 可在本机显式读取目标项目的 `.local` 文件，并且只导入目标 Provider gate、目标连接、reset 授权和 MySQL 跨库选项；settings 文件中的全局 gate 与其他 Provider 变量会被忽略，但进程环境中已有的冲突变量会由 preflight 拒绝。Provider CI 禁止读取 `.local`，也禁止 `ConnectionStrings__DefaultConnection` 回退。仓库当前不跟踪外部 Provider 的公共 `integration.runsettings` 模板。

Provider 在导入配置或 preflight 失败时仍以非零退出，并在 `artifacts/provider-test-results/<run>/provider-startup-diagnostic.json` 写入脱敏诊断。诊断只包含 Provider、测试程序集、TFM、配置来源、gate、连接是否配置、安全数据库名、reset、可达性、`ExecutionStatus` 和 `BlockedReason`；常见原因包括 `ProviderGateDisabled`、`SettingsMissing`、`ConnectionMissing`、`UnsafeDatabase`、`ResetDenied` 和 `Unreachable`。原始异常和连接字符串不会写入该制品，因此该 JSON 不能被解释为测试通过。

## 受保护 Provider CI

每个 Provider lane 必须在 build 完成后使用 `--no-build` 调用同一个 runner，并仅注入自身的 Provider gate、专属连接字符串和 `ALLOW_DATABASE_RESET_FOR_TESTS=true`：

```powershell
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider MySql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider PostgreSql -Framework net8.0 -Configuration Release
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -Provider SqlServer -Framework net8.0 -Configuration Release
```

本机使用未跟踪配置时，显式传入对应 Provider 的 Settings 文件：

```powershell
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 `
    -Provider PostgreSql -Framework net8.0 `
    -Settings .\framework\tests\Bing.Dapper.PostgreSql.Tests.Integration\integration.runsettings.local
```

如果 MySQL 服务器使用 `caching_sha2_password`，本机 Settings 必须提供受信 TLS，或由管理员为专用测试账号配置受控 RSA public key retrieval；runner 不会自动放宽 TLS/RSA 安全策略，也不会把认证失败标记为 Skip。

AppVeyor 的 environment matrix materialize `common`、`mysql`、`postgresql`、`sqlserver` 和 `release` lane；Provider 专属变量和密钥只能在对应受保护的远端作业中配置，缺少 secret 时 runner 必须失败而不是跳过。在远端 secret scope、trusted-lane 策略和安全测试库未完成前，Provider job 不能作为已验收证据。`release` lane 在同一个受保护 job 内完成 Provider、SQLite、Unit、Analyzer、FormalHost 和 aggregate；不再配置依赖跨 matrix job 工作区的独立 `aggregate` lane。

runner 在连接前验证规范 gate、专属连接字符串、reset 授权和安全测试数据库名，并为每个 Provider/TFM 写入独立 TRX/JSON 摘要。发现零测试、全部 Skip、core Provider Skip、全局 gate 或默认连接字符串时，runner 以非零退出。MySQL 跨库测试允许在未启用独立跨库配置时单独 Skip，不得掩盖 MySQL core 测试的执行结果。

## 验收重点

- Scope 创建的 Query 和 Executor 必须共享同一连接、事务和固定数据库上下文；
- 外部连接或事务身份不一致必须在执行前拒绝，且 Query 不提交、回滚或释放外部资源；
- 提交失败必须尝试回滚，两个失败原因均应保留；
- 每个 Provider 的 Provider、Query 和 Executor 注册均必须能够通过 `ISqlDbConnectionFactoryResolver` 创建对应独立连接；
- EF Shared 只复用 DbContext 的连接与当前事务，EF Independent 使用框架自有连接。

## Provider 合同证据

共享测试基建中的 `ProviderContractRunner` 只在执行委托成功且调用方提供完整 Provider/数据库/驱动版本、连接类别、测试方法、TRX、制品、UTC 时间和源码身份元数据时标记 `RealIntegrationProven`；没有该元数据的执行场景标记为 `UnitProven`。`Declared`、`Unsupported`、`ImplementationGap` 和 `NotExecuted` 必须由调用方明确提供，不能由静态 Profile、默认 Skip 或 runner self-test 推导为通过。执行场景与固定状态互斥，`ProviderCapabilityMatrix` 输出无密 Markdown/JSON，拒绝同一 Provider、能力和场景的重复记录，并把 `TestGenerated` 与可发布的 `ReleaseEvidence` 区分开。

SQLite 集成测试使用该合同验证真实 Scalar 和预取消场景。执行时必须为每次运行指定新的 `artifacts/test-results` 子目录；同一 `RunName` 同时派生 VSTest 的 TRX 文件名和 Matrix 文件名，测试进程通过受控环境变量接收实际结果目录，执行完成后脚本再校验两份制品属于同一次运行：

```powershell
.\eng\ci\Invoke-SqliteContractTests.ps1 `
	-ResultsDirectory artifacts/test-results/provider-capability-runs/sqlite/net8 `
	-Framework net8.0 `
	-Configuration Release `
	-RunName sqlite-contract-net8
```

脚本会拒绝越界路径、已存在的制品、缺失的 Matrix/TRX、错误的 `total=1/passed=1/failed=0/notExecuted=0` 计数、方法名或源码身份不一致，以及 Matrix 时间不在当前 TRX 执行窗口内。当前制品为 `TestGenerated`，不能单独放行发布门禁。MySQL、PostgreSQL、SQL Server、Oracle、Doris 仍需各自的 Provider gate、专用连接变量和安全 reset 条件。报告必须分别保存每个 TFM、每次运行的 TRX，不能用复用文件名的单个历史 TRX 冒充当前证据。
