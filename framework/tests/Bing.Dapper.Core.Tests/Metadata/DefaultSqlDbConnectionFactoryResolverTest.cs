using System.Data;
using Bing.Data.Enums;
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
        Assert.Throws<InvalidOperationException>(() => resolver.Create(DatabaseType.SqlServer, "Server=(local)"));
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
                DatabaseType = DatabaseType.Sqlite,
                Factory = connectionString =>
                {
                    receivedConnectionString = connectionString;
                    return connection;
                }
            }
        });

        // Act
        var result = resolver.Create(DatabaseType.Sqlite, "Data Source=test.db");

        // Assert
        Assert.Same(connection, result);
        Assert.Equal("Data Source=test.db", receivedConnectionString);
    }

    /// <summary>
    /// 测试目的：同一 Provider 重复注册时最后一个有效工厂应生效。
    /// </summary>
    [Fact]
    public void Create_WhenFactoryRegisteredRepeatedly_ShouldUseLatestFactory()
    {
        // Arrange
        var firstConnection = new Mock<IDbConnection>().Object;
        var latestConnection = new Mock<IDbConnection>().Object;
        var resolver = new DefaultSqlDbConnectionFactoryResolver(new[]
        {
            new SqlDbConnectionFactoryRegistration { DatabaseType = DatabaseType.MySql, Factory = _ => firstConnection },
            new SqlDbConnectionFactoryRegistration { DatabaseType = DatabaseType.MySql, Factory = _ => latestConnection }
        });

        // Act
        var result = resolver.Create(DatabaseType.MySql, "Server=localhost");

        // Assert
        Assert.Same(latestConnection, result);
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
            new SqlDbConnectionFactoryRegistration { DatabaseType = DatabaseType.Oracle, Factory = _ => null }
        });

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => resolver.Create(DatabaseType.Oracle, "Data Source=oracle"));
    }
}