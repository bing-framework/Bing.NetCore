using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存回源工厂的合并、准入、排队和递归保护行为。
/// </summary>
public class FusionCacheFactoryTest
{
    /// <summary>
    /// 验证同键回源会合并为一次调用，且缓存命中不占用回源准入额度。
    /// </summary>
    [Fact]
    public async Task SameKeyCoalesces_AndHitsDoNotUseAdmission()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o => { o.MaxConcurrentFactories = 1; o.MaxQueuedFactories = 0; }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var started = TestCache.Signal<bool>();
        var release = TestCache.Signal<int>();
        var calls = 0;
        var tasks = Enumerable.Range(0, 32).Select(_ => cache.GetOrCreateAsync("one", async token =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult(true);
            return await release.Task;
        })).ToArray();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cache.SetAsync("hit", 7);
            Assert.Equal(7, await cache.GetOrCreateAsync<int>("hit", _ => throw new Exception()));
            Assert.Equal(0, metrics.Waiting);
            Assert.Equal(1, metrics.Active);
        }
        finally { release.TrySetResult(42); }
        Assert.All(await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)), v => Assert.Equal(42, v));
        Assert.Equal(1, calls);
        Assert.Equal(0, metrics.Active);
    }

    /// <summary>
    /// 验证不同键遵守并发与排队上限，并在拒绝后恢复可用额度。
    /// </summary>
    [Fact]
    public async Task DifferentKeysRespectActiveAndQueueLimits_AndRecoverAfterRejection()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o => { o.MaxConcurrentFactories = 2; o.MaxQueuedFactories = 2; }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var release = TestCache.Signal<int>();
        var tasks = Enumerable.Range(0, 4).Select(i =>
            cache.GetOrCreateAsync("key" + i, _ => release.Task)).ToArray();
        try
        {
            await TestCache.Eventually(() => Task.FromResult(metrics.Active == 2 && metrics.Waiting == 2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync("rejected", _ => Task.FromResult(1)));
        }
        finally { release.TrySetResult(42); }
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, metrics.PeakActive);
        Assert.Equal(2, metrics.PeakWaiting);
        Assert.Equal(1, metrics.Rejected);
        Assert.Equal(0, metrics.Active);
        Assert.Equal(0, metrics.Waiting);
        Assert.Equal(5, await cache.GetOrCreateAsync("recovered", _ => Task.FromResult(5)));
    }

    /// <summary>
    /// 验证排队中的请求被取消后会释放排队额度。
    /// </summary>
    [Fact]
    public async Task QueuedCancellationReleasesQueueSlot()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o => { o.MaxConcurrentFactories = 1; o.MaxQueuedFactories = 1; }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var release = TestCache.Signal<int>();
        var active = cache.GetOrCreateAsync("active", _ => release.Task);
        using var cancellation = new CancellationTokenSource();
        await TestCache.Eventually(() => Task.FromResult(metrics.Active == 1));
        var queued = cache.GetOrCreateAsync("queued", _ => Task.FromResult(2), cancellationToken: cancellation.Token);
        try
        {
            await TestCache.Eventually(() => Task.FromResult(metrics.Waiting == 1));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            await TestCache.Eventually(() => Task.FromResult(metrics.Waiting == 0));
            var replacement = cache.GetOrCreateAsync("replacement", _ => Task.FromResult(3));
            release.TrySetResult(1);
            Assert.Equal(3, await replacement.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { release.TrySetResult(1); }
        await active;
    }

    /// <summary>
    /// 验证排队超时和工厂失败都会释放并发额度。
    /// </summary>
    [Fact]
    public async Task QueueTimeoutAndFactoryFailureReleaseCapacity()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o =>
        {
            o.MaxConcurrentFactories = 1; o.MaxQueuedFactories = 1;
            o.FactoryQueueTimeout = TimeSpan.FromMilliseconds(50);
        }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var release = TestCache.Signal<int>();
        var active = cache.GetOrCreateAsync("active", _ => release.Task);
        try
        {
            await TestCache.Eventually(() => Task.FromResult(metrics.Active == 1));
            await Assert.ThrowsAsync<TimeoutException>(() => cache.GetOrCreateAsync("queued", _ => Task.FromResult(2)));
        }
        finally { release.TrySetResult(1); }
        await active;
        await Assert.ThrowsAsync<FormatException>(() => cache.GetOrCreateAsync<int>("error", _ => throw new FormatException()));
        Assert.Equal(3, await cache.GetOrCreateAsync("recovered", _ => Task.FromResult(3)));
        Assert.Equal(0, metrics.Active);
        Assert.Equal(0, metrics.Waiting);
    }

    /// <summary>
    /// 验证工厂忽略取消并继续运行时仍持有额度，直到真正结束才释放。
    /// </summary>
    [Fact]
    public async Task IgnoringCancellationKeepsPermitUntilFactoryReallyEnds()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o => { o.MaxConcurrentFactories = 1; o.MaxQueuedFactories = 0; },
            b => b.WithDefaultEntryOptions(o => o.FactoryHardTimeout = TimeSpan.FromMilliseconds(80)), name);
        var cache = services.GetRequiredService<IHybridCache>();
        var release = TestCache.Signal<int>();
        try
        {
            await Assert.ThrowsAnyAsync<TimeoutException>(() => cache.GetOrCreateAsync("late", _ => release.Task));
            Assert.Equal(1, metrics.Active);
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync("other", _ => Task.FromResult(2)));
        }
        finally { release.TrySetResult(1); }
        await TestCache.Eventually(() => Task.FromResult(metrics.Active == 0));
        Assert.Equal(3, await cache.GetOrCreateAsync("recovered", _ => Task.FromResult(3)));
    }

    /// <summary>
    /// 验证同键与嵌套冷回源会立即失败，并正确恢复异步执行上下文。
    /// </summary>
    [Fact]
    public async Task RecursiveFactoriesFailPromptly_AndRestoreContext()
    {
        using var services = TestCache.Create(o => o.MaxConcurrentFactories = 1);
        var cache = services.GetRequiredService<IHybridCache>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync("same",
            _ => cache.GetOrCreateAsync("same", _ => Task.FromResult(1))).WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync("outer",
            _ => cache.GetOrCreateAsync("inner", _ => Task.FromResult(1))).WaitAsync(TimeSpan.FromSeconds(2)));
        await cache.SetAsync("hit", 7);
        Assert.Equal(7, await cache.GetOrCreateAsync("outer", _ => cache.GetOrCreateAsync<int>("hit", _ => throw new Exception())));
        Assert.Equal(9, await cache.GetOrCreateAsync("same", _ => Task.FromResult(9)));
    }
}
