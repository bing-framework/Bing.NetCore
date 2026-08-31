namespace Bing.EasyCaching;

/// <summary>
/// 定义注册 EasyCaching 缓存提供器和序列化器时使用的稳定标识。
/// </summary>
public static class CacheProviderKey
{
    /// <summary>
    /// 内存缓存提供器的注册名称，必须与 EasyCaching 注册名称匹配。
    /// </summary>
    public const string MemoryCache = "DefaultInMemory";

    /// <summary>
    /// Redis 缓存提供器的注册名称，必须与 EasyCaching 注册名称匹配。
    /// </summary>
    public const string RedisCache = "DefaultRedis";

    /// <summary>
    /// 二级 Hybrid 缓存提供器的注册名称，必须与 EasyCaching 注册名称匹配。
    /// </summary>
    public const string HybridCache = "DefaultHybrid";

    /// <summary>
    /// Redis 总线使用的提供器名称；与 <see cref="RedisCache"/> 共用同一个 Redis Provider。
    /// </summary>
    public const string RedisBus = "DefaultRedis";

    /// <summary>
    /// System.Text.Json 序列化提供程序的注册名称。
    /// </summary>
    public const string SystemTextJson = "SystemTextJson";
}
