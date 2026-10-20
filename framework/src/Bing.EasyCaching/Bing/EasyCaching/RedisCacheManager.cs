using Bing.Caching;

namespace Bing.EasyCaching;

/// <summary>
/// EasyCaching Redis缓存服务
/// </summary>
public class RedisCacheManager : CacheManager, IRedisCache
{
    /// <summary>
    /// 执行 Redis 缓存操作的提供器。
    /// </summary>
    private readonly IRedisCachingProvider _provider;

    /// <summary>
    /// 初始化一个 <see cref="RedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="factory">EasyCaching缓存提供器工厂</param>
    /// <param name="configuration">缓存实例配置</param>
    public RedisCacheManager(IEasyCachingProviderFactory factory, CacheConfiguration configuration)
        : base(factory?.GetCachingProvider(configuration?.ProviderName), configuration)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        _provider = factory.GetRedisProvider(configuration.ProviderName);
        SetCoordinator(new RedisLeaseCoordinator(factory.GetCachingProvider(configuration.ProviderName), configuration));
    }
}
