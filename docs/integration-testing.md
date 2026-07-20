# 数据库集成测试

SQLite 集成测试始终执行，使用临时文件数据库，不需要额外配置。`Bing.Dapper.Sqlite.Tests.Integration` 验证 Dapper 双文件路由和事务；`Bing.EntityFrameworkCore.Tests` 同时验证 EF Core Independent/Shared 模式在两个真实 SQLite 文件上的路由与跨文件拒绝。MySQL、PostgreSQL、SQL Server 和 Oracle 测试默认跳过；只有在本机或受保护 CI 中显式开启后才会连接外部数据库。

## 本地运行

1. 复制 `tests/runsettings/integration.<provider>.runsettings.example` 为同目录的 `integration.<provider>.local.runsettings`。
2. 将其中的 `replace-me` 改为本地测试账号，并确认数据库名是专用测试库。
3. 在 Visual Studio 中选择该 `.runsettings` 文件，或使用 `dotnet test --settings <path>` 运行对应项目。

本地配置文件被 `.gitignore` 忽略，不能提交密码或连接字符串。

## 门控变量

- `RUN_INTEGRATION_TESTS=true`：启用全部外部 Provider 集成测试。
- `RUN_MYSQL_INTEGRATION_TESTS=true`
- `RUN_POSTGRESQL_INTEGRATION_TESTS=true`
- `RUN_SQLSERVER_INTEGRATION_TESTS=true`
- `RUN_ORACLE_INTEGRATION_TESTS=true`

Provider 级变量只启用对应 Provider。多 Provider 路由测试只接受全局变量，并要求同时提供 MySQL、PostgreSQL 和 SQL Server 的连接配置。

连接字符串优先使用 `ConnectionStrings__<Provider>Connection`，例如 `ConnectionStrings__MySqlConnection`；未配置时可兼容回退到 `ConnectionStrings__DefaultConnection`。缺失配置会给出变量名称和示例 runsettings，不会显示密码。

## 数据库安全

外部测试初始化和清理前都会校验 Provider 已启用且数据库名符合测试库命名规则。系统数据库、生产数据库命名和不安全名称会被拒绝。

允许的专用数据库名必须以 `_test`、`_tests`、`_integration` 或 `_integration_test` 结尾；系统库以及名称中含独立 `prod`、`production`、`development` 环境标识的数据库会被拒绝。校验异常不得回显完整连接字符串或密码。

表级初始化和清理不执行删库。数据库级重置额外要求 `ALLOW_DATABASE_RESET_FOR_TESTS=true`，并且仍受安全数据库名校验保护。

## CI

常规 CI 显式关闭所有外部 Provider 门控，只运行无凭据测试和 SQLite 集成测试。外部数据库测试应放在独立、受保护的构建中：连接字符串通过 CI 密钥注入，Provider 门控只在该构建中设为 `true`，日志不得输出连接字符串或凭据。SQLite 测试必须在每个测试内创建和释放数据库 Scope，不能跨 `IAsyncLifetime.InitializeAsync` 与 `DisposeAsync` 保存 `AsyncLocal` Scope。

## SQLite 边界覆盖

SQLite 无需外部服务，除双文件路由、事务和流式资源释放外，还覆盖：

- `ExecutionContext` 中 Scope 的嵌套、并行、异常和取消恢复；
- 诊断事件从 Query 固定上下文输出 `DbKey`、映射配置；租户标识仅在显式启用时输出，一维数组参数按独立快照发布；
- EF Core Shared 模式在同一 SQLite 文件复用连接、跨文件和不同命名共享内存均拒绝复用；
- `Data Source=<name>;Mode=Memory;Cache=Shared` 的命名共享内存数据库在 EF Core Shared 模式下实际执行查询。

事务替身测试还覆盖 Provider 原生异步提交和回滚不会再同步重复执行，以及开始失败与 owner Query 清理失败的异常聚合。外部 Provider 集成项目仍须通过门控变量启用，且不得使用生产连接字符串。

命名共享内存数据库必须保持至少一个打开连接，直到测试完成；普通 `:memory:` 连接彼此独占，不能用于跨连接或 EF Core Shared 测试。两种场景均无需外部数据库服务。

## SQL 事务与连接回归

事务和连接 API 收敛的无外部依赖回归包括：

- `Bing.Data.Sql.Tests`：只读事务上下文、Scope API、内部资源 Accessor/Binder，以及旧 Manager、外部上下文和数据库工厂契约不存在的架构约束；
- `Bing.Dapper.SqlServer.Tests`：替身 ADO.NET 连接上的提交失败回滚、Scope lease、资源失效和五个 Provider 的连接工厂注册；
- `Bing.Dapper.Sqlite.Tests` 与 `Bing.Dapper.Sqlite.Tests.Integration`：真实 SQLite 文件上的连接所有权、Scope 提交/回滚和多数据库上下文固定；
- `Bing.EntityFrameworkCore.Tests`：Shared 内部资源绑定、顺序事务刷新，以及 Independent 连接工厂路径。

本地执行命令和外部 Provider 门控见 [数据库集成测试说明](testing/database-integration-tests.md)。
