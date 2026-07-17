# 阶段六：EF Core SQL 查询收敛

## 完成项

- Shared 模式继续复用当前 `DbContext` 连接，并通过动态委托读取当前 EF Core 事务。
- Independent 模式依据 `DbContext.Database.ProviderName` 选择对应的 SQL Provider，并由 `ISqlDbConnectionFactoryResolver` 创建独立连接。
- SQL Server、MySQL、PostgreSQL、SQLite 和 Oracle Provider 通过各自的 `*DatabaseFactory` 注册独立连接工厂，未使用反射构造连接。
- 独立连接通过 `ISqlQueryExternalContext.SetOwnedConnection` 交给 Query 生命周期释放，不共享 EF 连接或事务。
- `UnitOfWorkBase` 不再回退 `ServiceLocator` 获取服务提供程序，构造时必须显式注入。

## 测试

- Shared 模式复用连接和 EF 模型映射。
- Query 创建后开启 EF 事务时动态解析当前事务。
- Independent 模式使用相同 Provider 与连接字符串，但连接实例独立且没有 EF 事务。

## 验证

- `dotnet test .\framework\tests\Bing.EntityFrameworkCore.Tests\Bing.EntityFrameworkCore.Tests.csproj -nologo -v minimal`：3 passed。
- `Bing.Dapper.MySql`、`Bing.Dapper.PostgreSql`、`Bing.Dapper.Oracle` 和 `Bing.Dapper.Sqlite` Provider 项目构建通过。

## 风险

- 不受支持的 EF Core Provider 会抛出明确异常；接入新 Provider 时需同时注册其 SQL 数据源和独立连接工厂。

## 2026-07-17 Shared 物理身份校验

- Shared 模式在解析 Ambient `DatabaseContext` 和最终数据源后，分别比较 EF 连接与 SQL 数据源的物理数据库身份，而非直接比较原始连接字符串。
- 身份比较忽略密码、用户和连接池参数，识别 SQL Server 实例、MySQL/PostgreSQL 主机端口、Oracle 服务名，以及 SQLite 规范化文件路径；同服务器不同数据库会被明确拒绝。
- Independent 模式仍创建独立连接，不绑定 EF 事务。`Bing.EntityFrameworkCore.Tests` 已覆盖 Ambient 同库、不同库和最终路由数据源校验。