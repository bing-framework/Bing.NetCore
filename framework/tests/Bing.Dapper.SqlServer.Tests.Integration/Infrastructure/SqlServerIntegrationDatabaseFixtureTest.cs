using Bing.Data.Sql;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 集成数据库 fixture 的服务作用域合同测试。
/// </summary>
[Collection(SqlServerIntegrationDatabaseCollection.Name)]
public sealed class SqlServerIntegrationDatabaseFixtureTest
{
    private readonly SqlServerIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化 fixture 合同测试。
    /// </summary>
    /// <param name="fixture">SQL Server 集成数据库 fixture。</param>
    public SqlServerIntegrationDatabaseFixtureTest(SqlServerIntegrationDatabaseFixture fixture) =>
        _fixture = fixture;

    /// <summary>
    /// 测试目的：SQL 工厂不能从根容器解析，但必须能在显式作用域内创建和释放对象。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public void Fixture_WhenIntegrationEnabled_ShouldRequireExplicitServiceScope()
    {
        var rootProvider = _fixture.ServiceProvider;

        Assert.Throws<InvalidOperationException>(() => rootProvider.GetRequiredService<ISqlQueryFactory>());
        Assert.Throws<InvalidOperationException>(() => rootProvider.GetRequiredService<ISqlExecutorFactory>());
        Assert.Throws<InvalidOperationException>(() =>
            rootProvider.GetRequiredService<ISqlMultipleQueryExecutorFactory>());

        using var scope = _fixture.CreateScope();
        using var query = _fixture.CreateQuery(scope.ServiceProvider);
        using var executor = _fixture.CreateExecutor(scope.ServiceProvider);
        using var multipleExecutor = _fixture.CreateMultipleQueryExecutor(scope.ServiceProvider);

        Assert.NotNull(query);
        Assert.NotNull(executor);
        Assert.NotNull(multipleExecutor);
    }
}
