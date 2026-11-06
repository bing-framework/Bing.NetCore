using Bing.Caching;
using Xunit;

namespace Bing.Caching.ContractTests;

/// <summary>
/// 可由不同 Provider 复用的 IHybridCache 公共行为断言。
/// </summary>
public static class HybridCacheContractAssertions
{
    /// <summary>
    /// 验证单键读写、null 命中、同键合并、移除、软过期、TTL、标签失效和实例清理。
    /// </summary>
    /// <remarks>
    /// 必须传入隔离的测试实例；本方法最后调用 ClearAsync。
    /// </remarks>
    /// <param name="cache">待验证的混合缓存实现。</param>
    public static async Task VerifyAsync(IHybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var prefix = "hybrid-contract:" + Guid.NewGuid().ToString("N") + ":";
        var missingKey = prefix + "missing";
        var nullKey = prefix + "null";

        var missing = await cache.TryGetAsync<string?>(missingKey);
        Assert.False(missing.Hit);
        Assert.Equal(missingKey, missing.Key);

        await cache.SetAsync<string?>(nullKey, null);
        var nullHit = await cache.TryGetAsync<string?>(nullKey);
        Assert.True(nullHit.Hit);
        Assert.Null(nullHit.Value);
        Assert.Equal(nullKey, nullHit.Key);

        var nullFactoryCalls = 0;
        Assert.Null(await cache.GetOrCreateAsync<string?>(prefix + "negative", _ =>
        {
            Interlocked.Increment(ref nullFactoryCalls);
            return Task.FromResult<string?>(null);
        }));
        Assert.Null(await cache.GetOrCreateAsync<string?>(prefix + "negative", _ =>
        {
            Interlocked.Increment(ref nullFactoryCalls);
            return Task.FromResult<string?>("unexpected");
        }));
        Assert.Equal(1, nullFactoryCalls);

        var sharedKey = prefix + "shared";
        var factoryStarted = NewSignal<bool>();
        var releaseFactory = NewSignal<string>();
        var ownerCalls = 0;
        var waiterCalls = 0;
        var owner = cache.GetOrCreateAsync(sharedKey, async _ =>
        {
            Interlocked.Increment(ref ownerCalls);
            factoryStarted.TrySetResult(true);
            return await releaseFactory.Task.ConfigureAwait(false);
        });

        try
        {
            await factoryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var waiters = Enumerable.Range(0, 8).Select(_ => cache.GetOrCreateAsync(sharedKey, _ =>
            {
                Interlocked.Increment(ref waiterCalls);
                return Task.FromResult("unexpected");
            })).ToArray();

            Assert.All(waiters, task => Assert.False(task.IsCompleted));
            releaseFactory.TrySetResult("shared-value");
            Assert.Equal("shared-value", await owner.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.All(await Task.WhenAll(waiters).WaitAsync(TimeSpan.FromSeconds(10)),
                value => Assert.Equal("shared-value", value));
            Assert.Equal(1, ownerCalls);
            Assert.Equal(0, waiterCalls);
        }
        finally
        {
            releaseFactory.TrySetResult("shared-value");
            await owner.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var failingKey = prefix + "failure";
        await Assert.ThrowsAnyAsync<Exception>(() =>
            cache.GetOrCreateAsync<string>(failingKey, _ => Task.FromException<string>(new InvalidOperationException("factory failure"))));
        Assert.False((await cache.TryGetAsync<string>(failingKey)).Hit);
        Assert.Equal("recovered", await cache.GetOrCreateAsync(failingKey, _ => Task.FromResult("recovered")));

        await VerifyRemovalAndExpiryAsync(cache);
        await VerifyExplicitTtlAsync(cache);

        var taggedFirst = prefix + "tagged:first";
        var taggedSecond = prefix + "tagged:second";
        var untouched = prefix + "tagged:untouched";
        var tag = prefix + "tag";
        await cache.SetAsync(taggedFirst, "first", tags: [tag]);
        await cache.SetAsync(taggedSecond, "second", tags: [tag]);
        await cache.SetAsync(untouched, "untouched", tags: [prefix + "other-tag"]);

        await cache.InvalidateTagAsync(tag);
        Assert.False((await cache.TryGetAsync<string>(taggedFirst)).Hit);
        Assert.False((await cache.TryGetAsync<string>(taggedSecond)).Hit);
        Assert.Equal("untouched", (await cache.TryGetAsync<string>(untouched)).Value);

        var clearKey = prefix + "clear";
        await cache.SetAsync(clearKey, "before-clear");
        await cache.ClearAsync();
        Assert.False((await cache.TryGetAsync<string>(clearKey)).Hit);
        Assert.False((await cache.TryGetAsync<string>(untouched)).Hit);
    }

    /// <summary>
    /// 验证移除和软过期后读取未命中，且软过期的键可以重新回源。
    /// </summary>
    /// <param name="cache">待验证的混合缓存实例。</param>
    public static async Task VerifyRemovalAndExpiryAsync(IHybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var prefix = "hybrid-contract:invalidation:" + Guid.NewGuid().ToString("N") + ":";
        var removed = prefix + "removed";
        var expired = prefix + "expired";
        try
        {
            await cache.SetAsync(removed, 1);
            await cache.SetAsync(expired, 2);
            Assert.True((await cache.TryGetAsync<int>(removed)).Hit);
            Assert.True((await cache.TryGetAsync<int>(expired)).Hit);
            await cache.RemoveAsync(removed);
            Assert.False((await cache.TryGetAsync<int>(removed)).Hit);
            await cache.ExpireAsync(expired);
            Assert.False((await cache.TryGetAsync<int>(expired)).Hit);
            Assert.Equal(3, await cache.GetOrCreateAsync(expired, _ => Task.FromResult(3)));
            Assert.Equal(3, (await cache.TryGetAsync<int>(expired)).Value);
        }
        finally
        {
            await cache.RemoveAsync(removed);
            await cache.RemoveAsync(expired);
        }
    }

    /// <summary>
    /// 验证显式 TTL 到期后不可读取，且不会影响另一条长有效期的缓存。
    /// </summary>
    /// <param name="cache">待验证的混合缓存实例。</param>
    /// <param name="ttl">短期限，未指定时为一秒。</param>
    /// <param name="timeout">最大观察时间，未指定时为十秒。</param>
    public static async Task VerifyExplicitTtlAsync(IHybridCache cache, TimeSpan? ttl = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var duration = ttl ?? TimeSpan.FromSeconds(1);
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        if (duration <= TimeSpan.Zero || limit <= duration)
            throw new ArgumentOutOfRangeException(nameof(timeout), "观察时间必须大于正数 TTL。");

        var prefix = "hybrid-contract:ttl:" + Guid.NewGuid().ToString("N") + ":";
        var shortKey = prefix + "short";
        var longKey = prefix + "long";
        try
        {
            await cache.SetAsync(longKey, 1, new CacheOptions { Expiration = TimeSpan.FromMinutes(1) });
            await cache.SetAsync(shortKey, 2, new CacheOptions { Expiration = duration });
            Assert.True((await cache.TryGetAsync<int>(shortKey)).Hit);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var shortEntry = await cache.TryGetAsync<int>(shortKey);
            while (shortEntry.Hit && clock.Elapsed < limit)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                shortEntry = await cache.TryGetAsync<int>(shortKey);
            }
            Assert.False(shortEntry.Hit, "显式 TTL 在观察时限内未生效。");
            var longEntry = await cache.TryGetAsync<int>(longKey);
            Assert.True(longEntry.Hit);
            Assert.Equal(1, longEntry.Value);
        }
        finally
        {
            await cache.RemoveAsync(shortKey);
            await cache.RemoveAsync(longKey);
        }
    }

    /// <summary>
    /// 验证取消同键等待者时，主工厂和其他等待者仍能完成。
    /// </summary>
    /// <remarks>
    /// 仅用于明确承诺等待者独立取消的 Provider；IHybridCache 不要求所有实现采用此策略。
    /// </remarks>
    /// <param name="cache">待验证的混合缓存实例。</param>
    public static async Task VerifyWaiterCancellationAsync(IHybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var key = "hybrid-contract:cancellation:" + Guid.NewGuid().ToString("N");
        var started = NewSignal<bool>();
        var release = NewSignal<int>();
        var ownerToken = default(CancellationToken);
        var ownerCalls = 0;
        var waiterCalls = 0;
        var owner = cache.GetOrCreateAsync<int>(key, token =>
        {
            ownerToken = token;
            Interlocked.Increment(ref ownerCalls);
            started.TrySetResult(true);
            return release.Task;
        });

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var cancelled = cache.GetOrCreateAsync(key, _ =>
            {
                Interlocked.Increment(ref waiterCalls);
                return Task.FromResult(-1);
            }, cancellationToken: cancellation.Token);
            Assert.False(cancelled.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(ownerToken.IsCancellationRequested);

            var other = cache.GetOrCreateAsync(key, _ =>
            {
                Interlocked.Increment(ref waiterCalls);
                return Task.FromResult(-2);
            });
            Assert.False(other.IsCompleted);
            release.TrySetResult(42);
            Assert.Equal(42, await owner.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(42, await other.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, ownerCalls);
            Assert.Equal(0, waiterCalls);
            Assert.Equal(42, (await cache.TryGetAsync<int>(key)).Value);
        }
        finally
        {
            release.TrySetResult(42);
            await owner.WaitAsync(TimeSpan.FromSeconds(10));
            await cache.RemoveAsync(key);
        }
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
