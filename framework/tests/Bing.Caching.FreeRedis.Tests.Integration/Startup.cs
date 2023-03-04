using FreeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Bing.Caching.FreeRedis.Tests;

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
        var redisClient = new RedisClient("127.0.0.1:6379,database=0,idleTimeout=10000");
        // 配置默认使用Newtonsoft.Json作为序列化工具
        redisClient.Serialize = JsonConvert.SerializeObject;
        redisClient.Deserialize = JsonConvert.DeserializeObject;
        // 注入到IServiceCollection中
        //services.AddSingleton(redisClient);// 该注入方式，单元测试无法结束
        services.AddSingleton(x => redisClient);
        services.AddScoped<ICache, FreeRedisCacheManager>();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        // 添加单元测试日志提供程序，并配置日志过滤
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));

        var listener = new ActivityListener();
        listener.ShouldListenTo += _ => true;
        listener.Sample += delegate { return ActivitySamplingResult.AllDataAndRecorded; };

        ActivitySource.AddActivityListener(listener);
    }
}
