using Microsoft.Extensions.Configuration;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// PostgreSQL 集成测试启动配置测试。
/// </summary>
public sealed class PostgreSqlStartupConnectionStringTest
{
    /// <summary>
    /// 测试 - 专属连接字符串存在时应优先于本地默认连接字符串。
    /// </summary>
    [Fact]
    public void ResolveConnectionString_WhenProviderSpecificConnectionExists_ShouldPreferProviderSpecificConnection()
    {
        // Arrange
        var configuration = CreateConfiguration("provider-connection", "default-connection");

        // Act
        var result = Startup.ResolveConnectionString(configuration);

        // Assert
        Assert.Equal("provider-connection", result);
    }

    /// <summary>
    /// 测试 - 专属连接字符串缺失时应保留本地默认连接兼容。
    /// </summary>
    [Fact]
    public void ResolveConnectionString_WhenProviderSpecificConnectionIsMissing_ShouldFallbackToDefaultConnection()
    {
        // Arrange
        var configuration = CreateConfiguration(null, "default-connection");

        // Act
        var result = Startup.ResolveConnectionString(configuration);

        // Assert
        Assert.Equal("default-connection", result);
    }

    /// <summary>
    /// 创建包含 PostgreSQL 和默认连接字符串的内存配置。
    /// </summary>
    /// <param name="providerConnection">PostgreSQL 专用连接字符串。</param>
    /// <param name="defaultConnection">默认连接字符串。</param>
    /// <returns>已构建的内存配置。</returns>
    private static IConfiguration CreateConfiguration(string providerConnection, string defaultConnection) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:PostgreSqlConnection"] = providerConnection,
            ["ConnectionStrings:DefaultConnection"] = defaultConnection
        }).Build();
}
