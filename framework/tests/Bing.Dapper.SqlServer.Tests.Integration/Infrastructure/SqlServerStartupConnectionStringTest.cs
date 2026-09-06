using Bing.Data.Enums;
using Bing.Data.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 集成测试启动配置测试。
/// </summary>
[Collection(SqlServerEnvironmentVariableTestCollection.Name)]
public sealed class SqlServerStartupConnectionStringTest
{
    /// <summary>
    /// 测试 - 专属连接字符串存在时应优先于本地默认连接字符串。
    /// </summary>
    [Fact]
    public void ResolveSqlServerConnectionString_WhenProviderSpecificConnectionExists_ShouldPreferProviderSpecificConnection()
    {
        // Arrange
        var configuration = CreateConfiguration("provider-connection", "default-connection");

        // Act
        var result = Startup.ResolveSqlServerConnectionString(configuration);

        // Assert
        Assert.Equal("provider-connection", result);
    }

    /// <summary>
    /// 测试 - 专属连接字符串缺失时应保留本地默认连接兼容。
    /// </summary>
    [Fact]
    public void ResolveSqlServerConnectionString_WhenProviderSpecificConnectionIsMissing_ShouldFallbackToDefaultConnection()
    {
        // Arrange
        var configuration = CreateConfiguration(null, "default-connection");

        // Act
        var result = Startup.ResolveSqlServerConnectionString(configuration);

        // Assert
        Assert.Equal("default-connection", result);
    }

    /// <summary>
    /// 测试 - 显式全局兼容运行时应注册三个 Provider 的具名数据源。
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenGlobalMultiProviderRunEnabled_ShouldRegisterAllProviderDataSources()
    {
        // Arrange
        var originalGlobalGate = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        var originalSqlServerGate = Environment.GetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS");
        Environment.SetEnvironmentVariable("RUN_INTEGRATION_TESTS", "true");
        Environment.SetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS", null);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:MySqlConnection"] = "mysql-connection",
                ["ConnectionStrings:PostgreSqlConnection"] = "postgresql-connection",
                ["ConnectionStrings:SqlServerConnection"] = "sqlserver-connection"
            }).Build();
            var services = new ServiceCollection();
            var context = new HostBuilderContext(new Dictionary<object, object>()) { Configuration = configuration };

            // Act
            new Startup().ConfigureServices(services, context);
            using var serviceProvider = services.BuildServiceProvider();
            var resolver = serviceProvider.GetRequiredService<ISqlDataSourceResolver>();

            // Assert
            Assert.Equal(DatabaseType.MySql, resolver.Resolve("mysql").DatabaseType);
            Assert.Equal(DatabaseType.PgSql, resolver.Resolve("pgsql").DatabaseType);
            Assert.Equal(DatabaseType.SqlServer, resolver.Resolve("sqlserver").DatabaseType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUN_INTEGRATION_TESTS", originalGlobalGate);
            Environment.SetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS", originalSqlServerGate);
        }
    }

    /// <summary>
    /// 测试 - SQL Server 专属保护 lane 不应因残留全局 gate 注册其他 Provider。
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenSqlServerProviderLaneIsEnabled_ShouldNotRegisterOtherProviders()
    {
        // Arrange
        var originalGlobalGate = Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS");
        var originalSqlServerGate = Environment.GetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS");
        Environment.SetEnvironmentVariable("RUN_INTEGRATION_TESTS", "true");
        Environment.SetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS", "true");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:SqlServerConnection"] = "sqlserver-connection",
                ["ConnectionStrings:MySqlConnection"] = "mysql-connection",
                ["ConnectionStrings:PostgreSqlConnection"] = "postgresql-connection"
            }).Build();
            var services = new ServiceCollection();
            var context = new HostBuilderContext(new Dictionary<object, object>()) { Configuration = configuration };

            // Act
            new Startup().ConfigureServices(services, context);
            using var serviceProvider = services.BuildServiceProvider();
            var resolver = serviceProvider.GetRequiredService<ISqlDataSourceResolver>();

            // Assert
            Assert.Equal(DatabaseType.SqlServer, resolver.Resolve("default").DatabaseType);
            Assert.ThrowsAny<Exception>(() => resolver.Resolve("mysql"));
            Assert.ThrowsAny<Exception>(() => resolver.Resolve("pgsql"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUN_INTEGRATION_TESTS", originalGlobalGate);
            Environment.SetEnvironmentVariable("RUN_SQLSERVER_INTEGRATION_TESTS", originalSqlServerGate);
        }
    }

    private static IConfiguration CreateConfiguration(string providerConnection, string defaultConnection) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:SqlServerConnection"] = providerConnection,
            ["ConnectionStrings:DefaultConnection"] = defaultConnection
        }).Build();
}