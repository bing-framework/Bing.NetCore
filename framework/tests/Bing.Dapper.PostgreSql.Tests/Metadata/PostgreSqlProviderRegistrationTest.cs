using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations.Batching;
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
    /// 测试目的：PostgreSQL dollar-quoted 文本中的参数样式不得触发插值参数改名，
    /// 避免文本内容与实际绑定参数发生错误冲突。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenTokenAppearsOnlyInDollarQuotedText_ShouldKeepDefaultParameterName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("pgsql", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("pgsql");

        // Act
        var description = query.SqlInterpolated(
            $"Select $tag$@p0$tag$ Where Name = {"Bing"}");
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters);

        // Assert
        Assert.Equal("Select $tag$@p0$tag$ Where Name = @p0", description.CommandText);
        Assert.Equal("Bing", Assert.Single(parameters).Value);
        Assert.True(parameters.ContainsKey("p0"));
    }

    /// <summary>
    /// 测试目的：具名 PostgreSQL 数据源应创建 PostgreSQL 查询、执行器、多结果集执行器、方言和 Npgsql 连接。
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
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("pgsql");
        using var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create("pgsql");
        using var multipleQueryExecutor = provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>()
            .Create("pgsql");
        using var connection = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(PostgreSqlSqlProvider.Instance.Key, "Host=localhost;Database=test;");

        // Assert
        Assert.IsType<PostgreSqlQuery>(query);
        Assert.IsType<PostgreSqlExecutor>(executor);
        Assert.IsType<PostgreSqlMultipleQueryExecutor>(multipleQueryExecutor);
        Assert.IsType<PostgreSqlDialect>(provider.GetRequiredService<ISqlProviderResolver>()
            .Resolve(PostgreSqlSqlProvider.Instance.Key).Dialect);
        Assert.IsType<NpgsqlConnection>(connection);
        Assert.IsType<PostgreSqlBatchUpdateRenderer>(provider.GetRequiredService<ISqlBatchUpdateRenderer>());
        Assert.Equal("Host=localhost;Database=test;", connection.ConnectionString);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Execution.SupportsMultipleResultSets);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
        Assert.Equal(SqlQueryCapabilityState.Supported, PostgreSqlSqlProvider.Instance.Profile.Query.Cte);
    }
}