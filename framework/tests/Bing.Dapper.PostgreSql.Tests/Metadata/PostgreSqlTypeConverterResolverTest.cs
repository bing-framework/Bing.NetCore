using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// PostgreSql 类型转换器解析器测试
/// </summary>
public class PostgreSqlTypeConverterResolverTest
{
    /// <summary>
    /// 测试目的：注册 PostgreSql Provider 能力时，默认查询和执行器选项应使用 PgSql 数据库类型。
    /// </summary>
    [Fact]
    public void AddPostgreSqlProvider_ShouldConfigurePgSqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        using var provider = services.BuildServiceProvider();

        // Act
        var queryOptions = provider.GetRequiredService<SqlOptions<PostgreSqlQuery>>();
        var executorOptions = provider.GetRequiredService<SqlOptions<PostgreSqlExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.PgSql, queryOptions.DatabaseType);
        Assert.Equal(DatabaseType.PgSql, executorOptions.DatabaseType);
    }

    /// <summary>
    /// 测试目的：固定 Query Factory 应为 PostgreSql 数据源创建官方查询实现。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenPostgreSqlDataSourceRegistered_ShouldCreatePostgreSqlQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("default", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var query = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Assert
        Assert.IsType<PostgreSqlQuery>(query);
    }

    /// <summary>
    /// 测试目的：固定 Executor Factory 应为 PostgreSql 数据源创建官方执行器实现。
    /// </summary>
    [Fact]
    public void ExecutorFactory_WhenPostgreSqlDataSourceRegistered_ShouldCreatePostgreSqlExecutor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("default", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create();

        // Assert
        Assert.IsType<PostgreSqlExecutor>(executor);
    }

    /// <summary>
    /// 测试 - 类型转换器解析器应解析 PostgreSql 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("default", DatabaseType.PgSql, "Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = resolver.Resolve(DatabaseType.PgSql);

        // Assert
        Assert.IsType<PostgreSqlTypeConverter>(converter);
    }

    /// <summary>
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        public IDbConnection GetConnection() => null;
    }
}