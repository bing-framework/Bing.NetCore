using Bing.Caching.CSRedis;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis TTL 和回源恢复测试。
/// </summary>
/// <remarks>
/// 覆盖短 TTL、预取消回源和回源异常后的协调状态清理。
/// </remarks>
public sealed class TtlCancellationRecoveryTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建待测缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="TtlCancellationRecoveryTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供 TTL、取消和回源异常测试创建缓存管理器的 Redis 客户端。</param>
    public TtlCancellationRecoveryTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证同步和异步写入的过期时间。
    /// </summary>
    /// <remarks>
    /// 写入分别使用 500 毫秒和 1500 毫秒 TTL，并检查短 TTL 先过期、长 TTL 后过期。
    /// </remarks>
    [Fact]
    public async Task SyncAndAsyncWrites_ExpireAt500And1500Milliseconds()
    {
        var configuration = new CacheConfiguration("csredis-ttl-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new CSRedisCacheManager(_client, configuration);
        var shortSyncKey = "short-sync";
        var longSyncKey = "long-sync";
        var shortAsyncKey = "short-async";
        var longAsyncKey = "long-async";

        try
        {
            cache.Set(shortSyncKey, 1, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(500) });
            cache.Set(longSyncKey, 2, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(1500) });
            await cache.SetAsync(shortAsyncKey, 3,
                new CacheOptions { Expiration = TimeSpan.FromMilliseconds(500) });
            await cache.SetAsync(longAsyncKey, 4,
                new CacheOptions { Expiration = TimeSpan.FromMilliseconds(1500) });

            Assert.Equal(1, cache.Get<int>(shortSyncKey));
            Assert.Equal(2, cache.Get<int>(longSyncKey));
            Assert.Equal(3, await cache.GetAsync<int>(shortAsyncKey));
            Assert.Equal(4, await cache.GetAsync<int>(longAsyncKey));

            await Task.Delay(800);

            Assert.Null(cache.Get<int?>(shortSyncKey));
            Assert.Null(await cache.GetAsync<int?>(shortAsyncKey));
            Assert.Equal(2, cache.Get<int>(longSyncKey));
            Assert.Equal(4, await cache.GetAsync<int>(longAsyncKey));

            await Task.Delay(1000);

            Assert.Null(cache.Get<int?>(longSyncKey));
            Assert.Null(await cache.GetAsync<int?>(longAsyncKey));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// 验证预取消回源的处理和状态恢复。
    /// </summary>
    /// <remarks>
    /// 预取消请求不得调用回源工厂，后续读取应能重新回源并成功写入。
    /// </remarks>
    [Fact]
    public async Task PreCanceledRetrieval_DoesNotInvokeFactory_AndStateRecovers()
    {
        var configuration = new CacheConfiguration("csredis-cancel-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new CSRedisCacheManager(_client, configuration);
        var key = "pre-canceled";
        var invoked = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync<int>(key, _ =>
            {
                invoked = true;
                return Task.FromResult(1);
            }, cancellationToken: cancellation.Token));

            Assert.False(invoked);
            Assert.Null(cache.Get<int?>(key));
            Assert.Equal(2, await cache.GetAsync<int>(key, _ => Task.FromResult(2)));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// 验证回源异常后的协调状态恢复。
    /// </summary>
    /// <remarks>
    /// 同步和异步回源失败后，后续调用都应能再次执行并成功返回。
    /// </remarks>
    [Fact]
    public async Task RetrievalException_ReleasesState_ForSyncAndAsyncCalls()
    {
        var configuration = new CacheConfiguration("csredis-error-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new CSRedisCacheManager(_client, configuration);
        var syncKey = "sync-error";
        var asyncKey = "async-error";

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                cache.Get<int>(syncKey, () => throw new InvalidOperationException("sync retrieval failed")));
            Assert.Null(cache.Get<int?>(syncKey));
            Assert.Equal(2, cache.Get<int>(syncKey, () => 2));

            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<int>(asyncKey, _ =>
                Task.FromException<int>(new InvalidOperationException("async retrieval failed"))));
            Assert.Null(await cache.GetAsync<int?>(asyncKey));
            Assert.Equal(4, await cache.GetAsync<int>(asyncKey, _ => Task.FromResult(4)));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }
}
