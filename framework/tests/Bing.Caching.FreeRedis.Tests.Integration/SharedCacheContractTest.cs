using Bing.Caching.ContractTests;
using Bing.Caching.FreeRedis;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 执行 FreeRedis Provider 的共享缓存契约测试。
/// </summary>
public sealed class SharedCacheContractTest
{
    /// <summary>
    /// 用于执行共享缓存契约测试的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="SharedCacheContractTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public SharedCacheContractTest(RedisClient client) => _client = client;

    /// <summary>
    /// 验证 FreeRedis Provider 满足共享缓存契约。
    /// </summary>
    [Fact]
    public async Task Provider_SatisfiesSharedContract()
    {
        var configuration = new CacheConfiguration("contract-free-" + Guid.NewGuid().ToString("N"), "free");
        await CacheContractAssertions.VerifyAsync(new FreeRedisCacheManager(_client, configuration));
    }
}
