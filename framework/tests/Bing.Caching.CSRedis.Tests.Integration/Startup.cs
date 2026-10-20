using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using CSRedis;
using Bing.Test.Shared;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 配置 CSRedis 集成测试主机和服务。
/// </summary>
public class Startup
{
    /// <summary>
    /// 注册集成测试所需的主机配置。
    /// </summary>
    /// <param name="hostBuilder">待配置的主机构建器。</param>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
        });
    }

    /// <summary>
    /// 根据集成测试开关和 Redis 连接配置注册 CSRedis 测试依赖。
    /// </summary>
    /// <remarks>
    /// 提供程序未启用时跳过注册；缺少 Redis 连接配置时抛出异常。
    /// </remarks>
    /// <param name="services">用于注册 CSRedis 集成测试依赖的服务集合。</param>
    public void ConfigureServices(IServiceCollection services)
    {
        if (IntegrationTestGate.IsProviderEnabled() == false)
            return;
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");
        services.AddSingleton(new CSRedisClient(connectionString));
        services.AddSingleton(new CacheConfiguration("csredis-integration", "csredis"));
        services.AddScoped<ICache, CSRedisCacheManager>();
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
