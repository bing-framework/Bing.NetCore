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
/// 配置 FreeRedis 集成测试主机。
/// </summary>
public class Startup
{
    /// <summary>
    /// 配置集成测试主机。
    /// </summary>
    /// <param name="hostBuilder">待配置的主机构建器。</param>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
        });
    }

    /// <summary>
    /// 注册 FreeRedis 集成测试所需的依赖项。
    /// </summary>
    /// <param name="services">服务集合。</param>
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
        services.AddSingleton(new CacheConfiguration("bing-cache-freeredis-integration", "FreeRedis"));
        services.AddScoped<ICache, FreeRedisCacheManager>();
        // 日志
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
    }

    /// <summary>
    /// 配置测试日志和活动监听。
    /// </summary>
    /// <param name="loggerFactory">用于配置测试日志的日志工厂。</param>
    /// <param name="accessor">用于访问 xUnit 输出助手的访问器。</param>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        var listener = new ActivityListener();
        listener.ShouldListenTo += _ => true;
        listener.Sample += delegate { return ActivitySamplingResult.AllDataAndRecorded; };

        ActivitySource.AddActivityListener(listener);
    }
}
