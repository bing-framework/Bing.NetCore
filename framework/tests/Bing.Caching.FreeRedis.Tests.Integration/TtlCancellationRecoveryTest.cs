using Bing.Caching.FreeRedis;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// FreeRedis 的短 TTL、预取消和回源失败清理集成测试。
/// </summary>
public sealed class TtlCancellationRecoveryTest
{
    /// <summary>
    /// 执行 TTL 和回源取消清理测试的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="TtlCancellationRecoveryTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public TtlCancellationRecoveryTest(RedisClient client) => _client = client;

    /// <summary>
    /// 验证同步与异步写入在 500 毫秒和 1500 毫秒后过期。
    /// </summary>
    [Fact]
    public async Task SyncAndAsyncWrites_ExpireAt500And1500Milliseconds()
    {
        var configuration = new CacheConfiguration("freeredis-ttl-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new FreeRedisCacheManager(_client, configuration);
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
    /// 预先取消的回源不调用工厂，后续读取仍可回源成功。
    /// </summary>
    [Fact]
    public async Task PreCanceledRetrieval_DoesNotInvokeFactory_AndStateRecovers()
    {
        var configuration = new CacheConfiguration("freeredis-cancel-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new FreeRedisCacheManager(_client, configuration);
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
    /// 调用方取消会传播，即使回源忽略令牌，迟到结果也不会写入。
    /// </summary>
    [Fact]
    public async Task CallerCancellation_PropagatesOperationCanceled_WhenRetrieverIgnoresToken_AndRecovers()
    {
        var configuration = new CacheConfiguration("freeredis-cancel-running-" + Guid.NewGuid().ToString("N"), "redis",
            coordination: new CacheCoordinationOptions(
                leaseDuration: TimeSpan.FromSeconds(5),
                renewalInterval: TimeSpan.FromMilliseconds(500),
                waitTimeout: TimeSpan.FromSeconds(2),
                retrievalTimeout: TimeSpan.FromSeconds(2)));
        var cache = new FreeRedisCacheManager(_client, configuration);
        var key = "caller-canceled-running";
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();

        try
        {
            var retrieval = cache.GetAsync<int>(key, _ =>
            {
                started.SetResult(true);
                return pending.Task;
            }, cancellationToken: cancellation.Token);

            await started.Task;
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => retrieval);
            pending.TrySetResult(99);
            await Task.Delay(100);
            Assert.Null(cache.Get<int?>(key));

            Assert.Equal(2, await cache.GetAsync<int>(key, _ => Task.FromResult(2)));
            Assert.Equal(2, cache.Get<int>(key));
        }
        finally
        {
            pending.TrySetResult(99);
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// 同步和异步回源异常后均清理协调状态并允许后续调用。
    /// </summary>
    [Fact]
    public async Task RetrievalException_ReleasesState_ForSyncAndAsyncCalls()
    {
        var configuration = new CacheConfiguration("freeredis-error-" + Guid.NewGuid().ToString("N"), "redis");
        var cache = new FreeRedisCacheManager(_client, configuration);
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
