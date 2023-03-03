using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;

namespace Bing.Caching.FreeRedis.Tests.Integration;

/// <summary>
/// 启动配置
/// </summary>
public class Startup
{
    /// <summary>
    /// 配置主机
    /// </summary>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
        });
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICache, FreeRedisCacheManager>();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        // 添加单元测试日志提供程序，并配置日志过滤
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));
    }
}
