using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// PostgreSQL 集成测试数据库固定装置测试。
/// </summary>
public sealed class PostgreSqlIntegrationDatabaseFixtureTest
{
    /// <summary>
    /// 测试目的：PostgreSQL 集成服务注册在无效连接字符串下不应建立网络连接，且应解析 SQL 核心服务。
    /// </summary>
    [Fact]
    public void ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection()
    {
        // Arrange
        var services = new ServiceCollection();
        PostgreSqlIntegrationDatabaseFixture.AddPostgreSqlIntegrationTestServices(services,
            "Host=invalid;Database=bing_framework_postgresql_test;Username=test;Password=test;",
            "Host=invalid;Database=bing_framework_postgresql_test;Username=test;Password=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
        var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
        var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
        var databaseContextResolver = provider.GetRequiredService<ISqlDatabaseContextResolver>();
        var dataSourceResolver = provider.GetRequiredService<ISqlDataSourceResolver>();

        // Assert
        Assert.NotNull(queryFactory);
        Assert.NotNull(executorFactory);
        Assert.NotNull(transactionScopeFactory);
        Assert.NotNull(databaseContextResolver);
        Assert.NotNull(dataSourceResolver);
    }
}