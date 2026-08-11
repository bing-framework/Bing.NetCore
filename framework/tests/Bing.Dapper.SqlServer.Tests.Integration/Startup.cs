using AspectCore.Extensions.Hosting;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.SqlServer;
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
/// 2. 环境变量：ConnectionStrings__DefaultConnection（兼容旧配置）
/// 3. appsettings.Development.json（本地开发，不提交到 Git）
/// 4. appsettings.json（默认为空，确保无硬编码凭据）
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
        var connectionString = context.Configuration.GetConnectionString("SqlServerConnection") ??
                       context.Configuration.GetConnectionString("DefaultConnection");
        var mySqlConnectionString = context.Configuration.GetConnectionString("MySqlConnection");
        var postgreSqlConnectionString = context.Configuration.GetConnectionString("PostgreSqlConnection");

        // 如果连接字符串为空，SqlQuery/SqlExecutor 将无法正常工作，
        // 但 Startup 本身不应抛异常，测试通过 [IntegrationFact] 跳过机制保护。
        services.AddSqlServerProvider();
        services.AddMySqlProvider();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource("mysql", Bing.Data.Enums.DatabaseType.MySql, mySqlConnectionString);
        services.AddSqlDataSource("pgsql", Bing.Data.Enums.DatabaseType.PgSql, postgreSqlConnectionString);
        services.AddSqlDataSource("sqlserver", Bing.Data.Enums.DatabaseType.SqlServer, connectionString);
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
        services.AddBing();
    }
}
