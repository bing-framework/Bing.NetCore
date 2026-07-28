using Bing.Data;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// <see cref="DefaultSqlConnectionStringResolver"/> 单元测试。
/// </summary>
public class DefaultSqlConnectionStringResolverTest
{
    /// <summary>
    /// 测试目的：数据源直接提供连接字符串时应优先返回该值。
    /// </summary>
    [Fact]
    public void Resolve_WhenDirectConnectionStringProvided_ShouldPreferDirectValue()
    {
        // Arrange
        var resolver = new DefaultSqlConnectionStringResolver(new ConnectionStringCollection { ["reporting"] = "Server=registry" });
        var dataSource = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            ConnectionString = "Server=direct",
            ConnectionStringName = "reporting"
        };

        // Act
        var result = resolver.Resolve(dataSource);

        // Assert
        Assert.Equal("Server=direct", result);
    }

    /// <summary>
    /// 测试目的：数据源仅提供连接字符串名称时应从注册集合解析。
    /// </summary>
    [Fact]
    public void Resolve_WhenConnectionStringNameProvided_ShouldResolveRegisteredValue()
    {
        // Arrange
        var resolver = new DefaultSqlConnectionStringResolver(new ConnectionStringCollection { ["reporting"] = "Server=registry" });
        var dataSource = new SqlDataSourceDescriptor { Key = "reporting", ConnectionStringName = "reporting" };

        // Act
        var result = resolver.Resolve(dataSource);

        // Assert
        Assert.Equal("Server=registry", result);
    }

    /// <summary>
    /// 测试目的：命名连接不存在或为空时应抛出配置异常。
    /// </summary>
    [Fact]
    public void Resolve_WhenNamedConnectionIsMissingOrEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var resolver = new DefaultSqlConnectionStringResolver(new ConnectionStringCollection { ["empty"] = " " });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new SqlDataSourceDescriptor { Key = "missing", ConnectionStringName = "missing" }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new SqlDataSourceDescriptor { Key = "empty", ConnectionStringName = "empty" }));
    }

    /// <summary>
    /// 测试目的：未提供数据源或任何连接配置时应抛出配置异常。
    /// </summary>
    [Fact]
    public void Resolve_WhenDataSourceOrConnectionConfigurationMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var resolver = new DefaultSqlConnectionStringResolver();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(null));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new SqlDataSourceDescriptor { Key = "default" }));
    }
}