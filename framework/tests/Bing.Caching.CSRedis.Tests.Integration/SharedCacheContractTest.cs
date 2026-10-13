using Bing.Caching.ContractTests;
using Bing.Caching.CSRedis;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 共享缓存契约测试。
/// </summary>
public sealed class SharedCacheContractTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建契约测试缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="SharedCacheContractTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供契约测试创建缓存管理器的 Redis 客户端。</param>
    public SharedCacheContractTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证 CSRedis 缓存管理器满足共享缓存契约。
    /// </summary>
    [Fact]
    public async Task Provider_SatisfiesSharedContract()
    {
        var configuration = new CacheConfiguration("contract-cs-" + Guid.NewGuid().ToString("N"), "cs");
        await CacheContractAssertions.VerifyAsync(new CSRedisCacheManager(_client, configuration));
    }
}
