# 数据库集成测试

SQLite 集成测试始终执行，使用临时文件数据库，不需要额外配置。MySQL、PostgreSQL、SQL Server 和 Oracle 测试默认跳过；只有在本机或受保护 CI 中显式开启后才会连接外部数据库。

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

表级初始化和清理不执行删库。数据库级重置额外要求 `ALLOW_DATABASE_RESET_FOR_TESTS=true`，并且仍受安全数据库名校验保护。

## CI

常规 CI 显式关闭所有外部 Provider 门控，只运行无凭据测试和 SQLite 集成测试。外部数据库测试应放在独立、受保护的构建中：连接字符串通过 CI 密钥注入，Provider 门控只在该构建中设为 `true`，日志不得输出连接字符串或凭据。