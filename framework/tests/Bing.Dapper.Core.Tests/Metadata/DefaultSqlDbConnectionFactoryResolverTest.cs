using System.Data;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="DefaultSqlDbConnectionFactoryResolver"/> 单元测试。
/// </summary>
public class DefaultSqlDbConnectionFactoryResolverTest
{
    /// <summary>
    /// 测试目的：未注册目标 Provider 的连接工厂时应拒绝创建连接。
    /// </summary>
    [Fact]
    public void Create_WhenFactoryIsMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var resolver = new DefaultSqlDbConnectionFactoryResolver(null);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Create("bing.sqlserver", "Server=(local)"));
    }

    /// <summary>
    /// 测试目的：有效注册应接收原始连接字符串并返回工厂创建的连接实例。
    /// </summary>
    [Fact]
    public void Create_WhenFactoryRegistered_ShouldPassConnectionStringAndReturnConnection()
    {
        // Arrange
        var connection = new Mock<IDbConnection>().Object;
        string receivedConnectionString = null;
        var resolver = new DefaultSqlDbConnectionFactoryResolver(new[]
        {
            new SqlDbConnectionFactoryRegistration
            {
                ProviderKey = "test.sqlite",
                Factory = connectionString =>
                {
                    receivedConnectionString = connectionString;
                    return connection;
                }
            }
        });

        // Act
        var result = resolver.Create("TEST.SQLITE", "Data Source=test.db");

        // Assert
        Assert.Same(connection, result);
        Assert.Equal("Data Source=test.db", receivedConnectionString);
    }

    /// <summary>
    /// 测试目的：同一 Provider Key 重复注册时必须明确失败，不能依赖注册顺序静默覆盖。
    /// </summary>
    [Fact]
    public void Create_WhenFactoryRegisteredRepeatedly_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var firstConnection = new Mock<IDbConnection>().Object;
        var duplicateConnection = new Mock<IDbConnection>().Object;

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => new DefaultSqlDbConnectionFactoryResolver(new[]
        {
            new SqlDbConnectionFactoryRegistration { ProviderKey = "test.mysql", Factory = _ => firstConnection },
            new SqlDbConnectionFactoryRegistration { ProviderKey = "test.mysql", Factory = _ => duplicateConnection }
        }));
    }

    /// <summary>
    /// 测试目的：共享同一数据库类型的不同 Provider Key 必须各自解析连接工厂，避免自定义 Provider 相互覆盖。
    /// </summary>
    [Fact]
    public void Create_WhenProviderKeysShareDatabaseType_ShouldKeepFactoriesIsolated()
    {
        // Arrange
        var firstConnection = new Mock<IDbConnection>().Object;
        var secondConnection = new Mock<IDbConnection>().Object;
        var resolver = new DefaultSqlDbConnectionFactoryResolver(new[]
        {
            new SqlDbConnectionFactoryRegistration { ProviderKey = "custom.sqlite.first", Factory = _ => firstConnection },
            new SqlDbConnectionFactoryRegistration { ProviderKey = "custom.sqlite.second", Factory = _ => secondConnection }
        });

        // Act
        var first = resolver.Create("custom.sqlite.first", "Data Source=first.db");
        var second = resolver.Create("custom.sqlite.second", "Data Source=second.db");

        // Assert
        Assert.Same(firstConnection, first);
        Assert.Same(secondConnection, second);
    }

    /// <summary>
    /// 测试目的：工厂返回空连接时应拒绝继续执行，避免后续空引用错误。
    /// </summary>
    [Fact]
    public void Create_WhenFactoryReturnsNull_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var resolver = new DefaultSqlDbConnectionFactoryResolver(new[]
        {
            new SqlDbConnectionFactoryRegistration { ProviderKey = "test.oracle", Factory = _ => null }
        });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Create("test.oracle", "Data Source=oracle"));
    }
}