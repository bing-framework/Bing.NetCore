using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// PostgreSQL Provider 服务注册测试。
/// </summary>
public class PostgreSqlProviderRegistrationTest
{
    /// <summary>
    /// 测试目的：具名 PostgreSQL 数据源应创建 PostgreSQL 查询、执行器、方言和 Npgsql 连接。
    /// </summary>
    [Fact]
    public void Factories_WhenPgSqlDataSourceIsConfigured_ShouldResolvePostgreSqlServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("pgsql", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("pgsql");
        using var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create<ISqlExecutor>("pgsql");
        using var connection = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(DatabaseType.PgSql, "Host=localhost;Database=test;");

        // Assert
        Assert.IsType<PostgreSqlQuery>(query);
        Assert.IsType<PostgreSqlExecutor>(executor);
        Assert.IsType<PostgreSqlDialect>(((ISqlPartAccessor)query).Dialect);
        Assert.IsType<NpgsqlConnection>(connection);
        Assert.Equal("Host=localhost;Database=test;", connection.ConnectionString);
    }
}