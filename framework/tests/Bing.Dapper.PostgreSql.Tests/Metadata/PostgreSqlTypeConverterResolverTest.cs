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
    /// 测试目的：注册 PostgreSql 查询对象时应固定使用 PgSql 数据库类型，确保独立连接工厂解析到 Npgsql。
    /// </summary>
    [Fact]
    public void AddPostgreSqlQuery_ShouldConfigurePgSqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlQuery("Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlOptions<PostgreSqlQuery>>();

        // Assert
        Assert.Equal(DatabaseType.PgSql, options.DatabaseType);
    }

    /// <summary>
    /// 测试目的：注册 PostgreSql 执行器时应固定使用 PgSql 数据类型，避免回退为 SqlServer。
    /// </summary>
    [Fact]
    public void AddPostgreSqlExecutor_ShouldConfigurePgSqlDatabaseType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostgreSqlExecutor("Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlOptions<PostgreSqlExecutor>>();

        // Assert
        Assert.Equal(DatabaseType.PgSql, options.DatabaseType);
    }

    /// <summary>
    /// 测试 - 类型转换器解析器应解析 PostgreSql 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlQuery("Host=localhost;Database=test;");
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