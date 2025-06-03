using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

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
        RedisHelper.Initialization(new CSRedisClient("127.0.0.1:6379,database=0,idleTimeout=10000"));
        services.AddScoped<ICache, CSRedisCacheManager>();
        // 日志
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        var listener = new ActivityListener();
        listener.ShouldListenTo += _ => true;
        listener.Sample += delegate { return ActivitySamplingResult.AllDataAndRecorded; };

        ActivitySource.AddActivityListener(listener);
    }
}
