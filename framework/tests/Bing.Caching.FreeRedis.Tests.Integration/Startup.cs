using FreeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Bing.Test.Shared;

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
        if (IntegrationTestGate.IsProviderEnabled() == false)
            return;
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");
        // 注入到IServiceCollection中
        services.AddSingleton(x =>
        {
            var redisClient = new RedisClient(connectionString);
            // 配置默认使用Newtonsoft.Json作为序列化工具
            redisClient.Serialize = JsonConvert.SerializeObject;
            redisClient.Deserialize = JsonConvert.DeserializeObject;
            return redisClient;
        });
        services.AddScoped<ICache, FreeRedisCacheManager>();
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
