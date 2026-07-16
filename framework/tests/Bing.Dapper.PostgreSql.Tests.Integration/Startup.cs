using AspectCore.Extensions.Hosting;
using Bing.Dapper.PostgreSql;
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
/// 1. 环境变量：ConnectionStrings__DefaultConnection
/// 2. appsettings.Development.json（本地开发，不提交到 Git）
/// 3. appsettings.json（默认为空，确保无硬编码凭据）
/// 前置条件：
/// - PostgreSQL 实例可访问
/// - 数据库账号有 CREATE / DROP / DML 权限
/// CI 启用方式：设置环境变量 RUN_INTEGRATION_TESTS=true 并提供连接字符串
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
                builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                builder.AddEnvironmentVariables();
            });
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
        services.AddPostgreSqlQuery(connectionString ?? string.Empty);
        services.AddPostgreSqlExecutor(connectionString ?? string.Empty);
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
        services.AddBing();
    }
}
