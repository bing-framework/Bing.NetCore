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