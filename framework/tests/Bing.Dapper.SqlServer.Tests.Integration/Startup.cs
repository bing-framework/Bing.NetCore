using AspectCore.Extensions.Hosting;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.SqlServer;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bing.Dapper.Tests;

/// <summary>
/// 集成测试启动配置。
/// 连接字符串通过以下任一方式提供（优先级从高到低）：
/// 1. 环境变量：ConnectionStrings__SqlServerConnection
/// 2. appsettings.Development.json（本地开发，不提交到 Git）
/// 3. appsettings.json 的 SqlServerConnection 或 DefaultConnection（本地兼容）
/// 前置条件：
/// - SQL Server 实例可访问
/// - 数据库账号有 CREATE / DROP / DML 权限
/// CI 启用方式：设置环境变量 RUN_SQLSERVER_INTEGRATION_TESTS=true 并提供连接字符串
/// </summary>
public class Startup
{
    /// <summary>
    /// 配置主机
    /// </summary>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder
            .ConfigureDefaults(null)
            .UseServiceContext()
            .ConfigureAppConfiguration((_, builder) =>
            {
                // 允许通过 appsettings.Development.json 覆盖连接字符串（本地开发使用）
                builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                // 允许通过环境变量覆盖（CI/CD 使用）
                builder.AddEnvironmentVariables();
            });
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        var connectionString = ResolveSqlServerConnectionString(context.Configuration);

    // 如果连接字符串为空，SqlQuery/SqlExecutor 将无法正常工作，
    // 但 Startup 本身不应抛异常，测试通过 [IntegrationFact] 跳过机制保护。
        services.AddSqlServerProvider();
        services.AddSqlDataSource("default", DatabaseType.SqlServer, connectionString);
        if (IsGlobalMultiProviderRunEnabled())
        {
            services.AddMySqlProvider();
            services.AddPostgreSqlProvider();
            services.AddSqlDataSource("mysql", DatabaseType.MySql,
                ResolveConnectionString(context.Configuration, "MySqlConnection"));
            services.AddSqlDataSource("pgsql", DatabaseType.PgSql,
                ResolveConnectionString(context.Configuration, "PostgreSqlConnection"));
            services.AddSqlDataSource("sqlserver", DatabaseType.SqlServer, connectionString);
        }
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
        services.AddBing();
    }

    /// <summary>
    /// 解析 SQL Server 连接字符串，优先使用专属配置，保留本地默认连接兼容。
    /// </summary>
    /// <param name="configuration">配置。</param>
    /// <returns>SQL Server 连接字符串。</returns>
    internal static string ResolveSqlServerConnectionString(IConfiguration configuration) =>
        ResolveConnectionString(configuration, "SqlServerConnection");

    /// <summary>
    /// 解析指定 Provider 连接字符串，未配置专属键时回退本地默认连接。
    /// </summary>
    /// <param name="configuration">配置。</param>
    /// <param name="connectionName">Provider 专属连接名称。</param>
    /// <returns>Provider 连接字符串。</returns>
    private static string ResolveConnectionString(IConfiguration configuration, string connectionName) =>
        configuration.GetConnectionString(connectionName) ?? configuration.GetConnectionString("DefaultConnection");

    /// <summary>
    /// 判断是否为本地显式的全局多 Provider 兼容运行。
    /// </summary>
    /// <returns>是全局兼容运行时返回 true。</returns>
    private static bool IsGlobalMultiProviderRunEnabled() => string.Equals(
        Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
}
