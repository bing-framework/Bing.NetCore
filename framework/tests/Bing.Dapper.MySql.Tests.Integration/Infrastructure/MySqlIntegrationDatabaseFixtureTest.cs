using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
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
    /// 初始化一个<see cref="MySqlIntegrationDatabaseFixtureTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">MySQL 集成测试数据库固定装置。</param>
    public MySqlIntegrationDatabaseFixtureTest(MySqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试 - MySQL 集成服务注册不连接数据库也应解析 SQL 核心服务。
    /// </summary>
    [Fact]
    public void ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection()
    {
        // Arrange
        var services = new ServiceCollection();
        MySqlIntegrationDatabaseFixture.AddMySqlIntegrationTestServices(services,
            "Server=invalid;Database=bing_dapper_test;User Id=test;Password=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
        var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
        var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
        var databaseContextResolver = provider.GetRequiredService<ISqlDatabaseContextResolver>();
        var dataSourceResolver = provider.GetRequiredService<ISqlDataSourceResolver>();
        var entityMappingResolver = provider.GetRequiredService<IEntityMappingResolver>();
        var entityModelMetadataProvider = provider.GetRequiredService<IEntityModelMetadataProvider>();

        // Assert
        Assert.NotNull(queryFactory);
        Assert.NotNull(executorFactory);
        Assert.NotNull(transactionScopeFactory);
        Assert.NotNull(databaseContextResolver);
        Assert.NotNull(dataSourceResolver);
        Assert.NotNull(entityMappingResolver);
        Assert.IsAssignableFrom<MySqlUnitOfWork>(entityModelMetadataProvider);
        Assert.Equal("ProductId", entityModelMetadataProvider.GetColumnName(typeof(Product), nameof(Product.Id)));
    }

    /// <summary>
    /// 测试目的：固定装置应使用唯一根服务容器解析 SQL 工厂和独立对象。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public void Fixture_WhenIntegrationEnabled_ShouldResolveSqlFactoriesFromSingleRootProvider()
    {
        Assert.Equal(1, _fixture.RootServiceProviderCreationCount);
        Assert.NotNull(_fixture.GetQueryFactory());
        Assert.NotNull(_fixture.GetExecutorFactory());
        Assert.NotNull(_fixture.GetTransactionScopeFactory());
        using var query = _fixture.CreateQuery();
        using var executor = _fixture.CreateExecutor();
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