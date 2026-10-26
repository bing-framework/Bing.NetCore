using Bing.Caching;
using Bing.Caching.FusionCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bing.Caching.FusionCache.Probe;

/// <summary>
/// 面向真实 Redis 集成测试的宿主工厂。
/// </summary>
/// <remarks>
/// 各原生组件使用依赖注入所有的独立连接，测试结束后由容器统一释放。
/// </remarks>
public static class RedisTestHost
{
    /// <summary>
    /// 创建启用 Redis L2、Backplane 及可选分布式锁的测试服务提供程序。
    /// </summary>
    /// <param name="cacheNamespace">缓存命名空间，用于隔离本次测试数据。</param>
    /// <param name="name">缓存实例名称，默认 <c>catalog</c>。</param>
    /// <param name="distributedLock">是否启用 Redis 分布式锁。</param>
    /// <param name="failSafe">是否启用 Fail-Safe。</param>
    /// <param name="onlyL2">是否跳过 L1 读写以仅验证分布式层。</param>
    /// <param name="rethrow">是否重新抛出分布式缓存和 Backplane 异常。</param>
    /// <param name="configure">覆盖原生 FusionCache 配置的可选委托。</param>
    /// <param name="configureLocker">覆盖分布式锁配置的可选委托。</param>
    /// <param name="decorateLocker">透明包装真实 Redis locker 的可选工厂。</param>
    /// <returns>已解析默认混合缓存的测试服务提供程序。</returns>
    /// <exception cref="InvalidOperationException">缺少 <c>BING_TEST_FUSIONCACHE_REDIS</c> 环境变量时抛出。</exception>
    public static ServiceProvider Create(string cacheNamespace, string name = "catalog",
        bool distributedLock = false, bool failSafe = false, bool onlyL2 = false,
        bool rethrow = true, Action<FusionCacheOptions>? configure = null,
        Action<RedisDistributedLockerOptions>? configureLocker = null,
        Func<IFusionCacheDistributedLocker, IFusionCacheDistributedLocker>? decorateLocker = null)
    {
        var connection = Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_REDIS")
            ?? throw new InvalidOperationException("BING_TEST_FUSIONCACHE_REDIS is required.");
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connection));
        services.AddSingleton<IDistributedCache>(_ => new RedisCache(Microsoft.Extensions.Options.Options.Create(new RedisCacheOptions
        {
            Configuration = connection,
            InstanceName = ""
        })));
        services.AddSingleton(_ => new RedisBackplane(Microsoft.Extensions.Options.Options.Create(new RedisBackplaneOptions { Configuration = connection })));
        if (distributedLock)
        {
            services.AddSingleton(_ =>
            {
                var options = new RedisDistributedLockerOptions { Configuration = connection };
                configureLocker?.Invoke(options);
                return new RedisDistributedLocker(Microsoft.Extensions.Options.Options.Create(options));
            });
        }
        services.AddBingFusionCache(name, o =>
        {
            o.Namespace = cacheNamespace;
            o.UseAsDefault = true;
        }, b =>
        {
            b.WithSerializer(new FusionCacheSystemTextJsonSerializer())
                .WithRegisteredDistributedCache()
                .WithBackplane(sp => sp.GetRequiredService<RedisBackplane>())
                .WithOptions(o =>
                {
                    o.AutoRecoveryDelay = TimeSpan.FromMilliseconds(200);
                    o.DistributedCacheCircuitBreakerDuration = TimeSpan.FromMilliseconds(100);
                    o.BackplaneCircuitBreakerDuration = TimeSpan.FromMilliseconds(100);
                    configure?.Invoke(o);
                })
                .WithDefaultEntryOptions(o =>
                {
                    o.IsFailSafeEnabled = failSafe;
                    o.FailSafeMaxDuration = TimeSpan.FromMinutes(2);
                    o.FailSafeThrottleDuration = TimeSpan.FromMilliseconds(50);
                    o.ReThrowDistributedCacheExceptions = rethrow;
                    o.ReThrowBackplaneExceptions = rethrow;
                    o.SkipMemoryCacheRead = onlyL2;
                    o.SkipMemoryCacheWrite = onlyL2;
                });
            if (distributedLock)
                b.WithDistributedLocker(sp =>
                {
                    // 原始 locker 仅由其具体类型注册负责释放，包装器不再重复持有所有权。
                    var locker = sp.GetRequiredService<RedisDistributedLocker>();
                    return decorateLocker?.Invoke(locker) ?? locker;
                });
        });
        var result = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        try { _ = result.GetRequiredService<IHybridCache>(); return result; }
        catch { result.Dispose(); throw; }
    }

    /// <summary>
    /// 生成用于记录回源调用次数的 Redis 键。
    /// </summary>
    /// <param name="cacheNamespace">当前测试的缓存命名空间。</param>
    /// <param name="key">业务缓存键。</param>
    /// <returns>隔离到命名空间和业务键的计数器键。</returns>
    public static string CounterKey(string cacheNamespace, string key) => $"probe:{cacheNamespace}:calls:{key}";
}
