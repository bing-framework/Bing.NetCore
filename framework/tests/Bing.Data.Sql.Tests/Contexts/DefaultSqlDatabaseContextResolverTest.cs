using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests.Contexts;

/// <summary>
/// <see cref="DefaultSqlDatabaseContextResolver"/> 单元测试。
/// </summary>
public class DefaultSqlDatabaseContextResolverTest
{
    /// <summary>
    /// 测试目的：SqlOptions 显式上下文应优先于访问器和默认上下文。
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitOptionsContextExists_ShouldPreferItAndReturnDeepClone()
    {
        // Arrange
        var explicitContext = CreateContext("explicit", DatabaseType.Sqlite);
        var accessor = new Mock<IDatabaseContextAccessor>();
        accessor.SetupGet(item => item.Current).Returns(CreateContext("ambient", DatabaseType.MySql));
        var metadataOptions = new SqlMetadataOptions { DefaultDatabaseContext = CreateContext("default", DatabaseType.SqlServer) };
        var resolver = new DefaultSqlDatabaseContextResolver(accessor.Object, metadataOptions);
        var options = new SqlOptions().SetDatabaseContext(explicitContext);

        // Act
        var result = resolver.Resolve(options);
        result.DataSource.Key = "changed";

        // Assert
        Assert.Equal("explicit", result.DbKey);
        Assert.Equal(DatabaseType.Sqlite, result.DataSource.DatabaseType);
        Assert.NotSame(explicitContext, result);
        Assert.NotSame(explicitContext.DataSource, result.DataSource);
        Assert.Equal("source-explicit", explicitContext.DataSource.Key);
    }

    /// <summary>
    /// 测试目的：未提供显式上下文时应优先使用访问器中的环境上下文。
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitContextMissing_ShouldUseAccessorContext()
    {
        // Arrange
        var ambientContext = CreateContext("ambient", DatabaseType.PgSql);
        var accessor = new Mock<IDatabaseContextAccessor>();
        accessor.SetupGet(item => item.Current).Returns(ambientContext);
        var resolver = new DefaultSqlDatabaseContextResolver(accessor.Object,
            new SqlMetadataOptions { DefaultDatabaseContext = CreateContext("default", DatabaseType.SqlServer) });

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Equal("ambient", result.DbKey);
        Assert.Equal(DatabaseType.PgSql, result.DataSource.DatabaseType);
        Assert.NotSame(ambientContext, result);
    }

    /// <summary>
    /// 测试目的：显式和环境上下文均不存在时应返回默认上下文的独立副本。
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyDefaultContextExists_ShouldUseDefaultContextClone()
    {
        // Arrange
        var defaultContext = CreateContext("default", DatabaseType.Oracle);
        var resolver = new DefaultSqlDatabaseContextResolver(options: new SqlMetadataOptions { DefaultDatabaseContext = defaultContext });

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Equal("default", result.DbKey);
        Assert.Equal(DatabaseType.Oracle, result.DataSource.DatabaseType);
        Assert.NotSame(defaultContext, result);
    }

    /// <summary>
    /// 测试目的：所有上下文来源均为空时应返回 null。
    /// </summary>
    [Fact]
    public void Resolve_WhenNoContextExists_ShouldReturnNull()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseContextResolver(options: new SqlMetadataOptions { DefaultDatabaseContext = null });

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 创建用于优先级测试的数据库上下文。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>包含独立数据源描述的上下文。</returns>
    private static DatabaseContext CreateContext(string dbKey, DatabaseType databaseType) => new()
    {
        DbKey = dbKey,
        DataSource = new SqlDataSourceDescriptor
        {
            Key = $"source-{dbKey}",
            DatabaseType = databaseType
        }
    };
}