using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.Datas.EntityFramework.Core;
using Bing.Tests.Models;
using Bing.Tests.UnitOfWorks;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 集成测试数据库固定装置测试。
/// </summary>
[Collection(MySqlIntegrationDatabaseCollection.Name)]
public sealed class MySqlIntegrationDatabaseFixtureTest
{
    /// <summary>
    /// MySQL 集成测试数据库固定装置。
    /// </summary>
    private readonly MySqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个 <see cref="MySqlIntegrationDatabaseFixtureTest"/> 类型的实例。
    /// </summary>
    /// <param name="fixture">MySQL 集成测试数据库固定装置。</param>
    public MySqlIntegrationDatabaseFixtureTest(MySqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：固定装置应使用唯一根服务容器解析 SQL 工厂和独立对象。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public void Fixture_WhenIntegrationEnabled_ShouldResolveSqlFactoriesFromSingleRootProvider()
    {
        Assert.Equal(1, _fixture.RootServiceProviderCreationCount);
        Assert.Throws<InvalidOperationException>(() =>
            _fixture.ServiceProvider.GetRequiredService<ISqlQueryFactory>());
        using var scope = _fixture.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        Assert.NotNull(_fixture.GetQueryFactory(serviceProvider));
        Assert.NotNull(_fixture.GetExecutorFactory(serviceProvider));
        Assert.NotNull(_fixture.GetTransactionScopeFactory(serviceProvider));
        using var query = _fixture.CreateQuery(serviceProvider);
        using var executor = _fixture.CreateExecutor(serviceProvider);
        Assert.NotNull(query);
        Assert.NotNull(executor);
    }

    /// <summary>
    /// 测试目的：MySQL 测试服务应能从同一根容器解析 SQL 核心依赖。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public void Fixture_WhenIntegrationEnabled_ShouldResolveSqlCoreServices()
    {
        Assert.NotNull(_fixture.ServiceProvider.GetService(typeof(ISqlDatabaseContextResolver)));
        Assert.NotNull(_fixture.ServiceProvider.GetService(typeof(ISqlDataSourceResolver)));
        Assert.NotNull(_fixture.ServiceProvider.GetService(typeof(IEntityMappingResolver)));
    }
}

/// <summary>
/// 不连接外部数据库的 MySQL 服务注册测试。
/// </summary>
public sealed class MySqlServiceRegistrationTest
{
    /// <summary>
    /// MySQL 集成服务注册不连接数据库也应解析 SQL 核心服务。
    /// </summary>
    [Fact]
    public void ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection()
    {
        var services = new ServiceCollection();
        MySqlIntegrationDatabaseFixture.AddMySqlIntegrationTestServices(services,
            "Server=invalid;Database=bing_dapper_test;User Id=test;Password=test;");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.NotNull(scopedProvider.GetRequiredService<ISqlQueryFactory>());
        Assert.NotNull(scopedProvider.GetRequiredService<ISqlExecutorFactory>());
        Assert.NotNull(scopedProvider.GetRequiredService<ISqlTransactionScopeFactory>());
        Assert.NotNull(scopedProvider.GetRequiredService<ISqlDatabaseContextResolver>());
        Assert.NotNull(scopedProvider.GetRequiredService<ISqlDataSourceResolver>());
        Assert.NotNull(scopedProvider.GetRequiredService<IEntityMappingResolver>());

        var entityModelMetadataProvider = scopedProvider.GetRequiredService<IEntityModelMetadataProvider>();
        Assert.IsType<EfCoreEntityModelMetadataProvider>(entityModelMetadataProvider);
        Assert.Equal("ProductId", entityModelMetadataProvider.GetMetadata(typeof(Product))
            .Properties[nameof(Product.Id)].ColumnName);
    }
}
