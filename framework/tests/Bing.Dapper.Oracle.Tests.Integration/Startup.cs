using AspectCore.Extensions.Hosting;
using Bing.Dapper.Oracle;
using Bing.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;

namespace Bing.Dapper.Tests;

/// <summary>
/// Oracle 集成测试启动配置。
/// </summary>
public class Startup
{
    /// <summary>
    /// 配置测试主机。
    /// </summary>
    /// <param name="hostBuilder">主机构建器。</param>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureDefaults(null)
            .UseServiceContext()
            .ConfigureAppConfiguration((_, builder) => builder.AddEnvironmentVariables());
    }

    /// <summary>
    /// 配置 Oracle 集成测试服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="context">主机构建上下文。</param>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        var connectionString = IntegrationTestGate.IsProviderEnabled("Oracle")
            ? IntegrationTestConnectionStringResolver.Resolve("Oracle")
            : string.Empty;
        services.AddOracleSqlQuery(connectionString);
        services.AddOracleSqlExecutor(connectionString);
        services.AddLogging(builder => builder.AddXunitOutput());
        services.AddBing();
    }
}