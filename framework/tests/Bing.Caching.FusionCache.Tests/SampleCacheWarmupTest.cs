using Bing.Samples.FusionCache;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证缓存预热样例的分批调度、取消和结果统计。
/// </summary>
public class SampleCacheWarmupTest
{
    /// <summary>
    /// 验证每批并发不超过配置值，且上一批完成后才启动下一批。
    /// </summary>
    [Fact]
    public async Task RunAsync_LimitsConcurrencyAndWaitsBetweenBatches()
    {
        var name = Guid.NewGuid().ToString("N");
        using var services = TestCache.Create(o =>
        {
            o.MaxConcurrentFactories = 2;
            o.MaxQueuedFactories = 0;
        }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var firstBatchEntered = TestCache.Signal<bool>();
        var secondBatchEntered = TestCache.Signal<bool>();
        var releaseFirstBatch = TestCache.Signal<bool>();
        var releaseSecondBatch = TestCache.Signal<bool>();
        var keys = new[] { "batch-0", "batch-1", "batch-2", "batch-3" };
        var active = 0;
        var peakActive = 0;
        var firstBatchStarted = 0;
        var secondBatchStarted = 0;

        async Task<string> Load(string key, CancellationToken _)
        {
            var current = Interlocked.Increment(ref active);
            UpdatePeak(ref peakActive, current);
            try
            {
                if (key is "batch-0" or "batch-1")
                {
                    if (Interlocked.Increment(ref firstBatchStarted) == 2)
                        firstBatchEntered.TrySetResult(true);
                    await releaseFirstBatch.Task;
                }
                else
                {
                    if (Interlocked.Increment(ref secondBatchStarted) == 2)
                        secondBatchEntered.TrySetResult(true);
                    await releaseSecondBatch.Task;
                }

                return key;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        var run = SampleCacheWarmup.RunAsync(cache, keys, Load, batchSize: 2, budget: TimeSpan.FromSeconds(10));
        try
        {
            await firstBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, Volatile.Read(ref active));
            Assert.Equal(0, Volatile.Read(ref secondBatchStarted));
            Assert.False(secondBatchEntered.Task.IsCompleted);

            releaseFirstBatch.TrySetResult(true);
            await secondBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, Volatile.Read(ref active));
            Assert.Equal(2, Volatile.Read(ref peakActive));
        }
        finally
        {
            releaseFirstBatch.TrySetResult(true);
            releaseSecondBatch.TrySetResult(true);
        }

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, result.Started);
        Assert.Equal(4, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Cancelled);
        Assert.Equal(0, result.NotStarted);
        AssertStatisticsConserved(result);
    }

    /// <summary>
    /// 验证单键回源失败后仍继续处理后续键，且缓存命中不会调用回源委托。
    /// </summary>
    [Fact]
    public async Task RunAsync_ContinuesAfterSingleKeyFailureAndSkipsFactoryOnHit()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("already-warm", "cached-value");
        var factoryCalls = 0;

        Task<string> Load(string key, CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            return key == "failure"
                ? Task.FromException<string>(new InvalidOperationException("sample failure"))
                : Task.FromResult("loaded-value");
        }

        var result = await SampleCacheWarmup.RunAsync(cache,
            ["failure", "already-warm", "after-failure"], Load, batchSize: 1, budget: TimeSpan.FromSeconds(5));

        Assert.Equal(2, factoryCalls);
        Assert.Equal(3, result.Requested);
        Assert.Equal(3, result.Started);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Cancelled);
        Assert.Equal(0, result.NotStarted);
        var hit = await cache.TryGetAsync<string>("already-warm");
        Assert.True(hit.Hit);
        Assert.Equal("cached-value", hit.Value);
        AssertStatisticsConserved(result);
    }

    /// <summary>
    /// 验证调用方取消会停止后续批次、记录已取消的当前键并释放回源额度。
    /// </summary>
    [Fact]
    public async Task RunAsync_CallerCancellationStopsLaterBatchesAndReleasesFactoryPermit()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o =>
        {
            o.MaxConcurrentFactories = 1;
            o.MaxQueuedFactories = 0;
        }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        using var cancellation = new CancellationTokenSource();
        var factoryEntered = TestCache.Signal<bool>();
        var release = TestCache.Signal<bool>();
        var factoryCalls = 0;

        async Task<string> Load(string _, CancellationToken token)
        {
            Interlocked.Increment(ref factoryCalls);
            factoryEntered.TrySetResult(true);
            await release.Task.WaitAsync(token);
            return "loaded-value";
        }

        var run = SampleCacheWarmup.RunAsync(cache, ["first", "second", "third"], Load,
            batchSize: 1, budget: TimeSpan.FromSeconds(10), cancellationToken: cancellation.Token);
        await factoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, factoryCalls);
        Assert.True(result.CallerCancelled);
        Assert.False(result.TimedOut);
        Assert.Equal(1, result.Started);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(2, result.NotStarted);
        Assert.Equal(0, metrics.Active);
        AssertStatisticsConserved(result);
    }

    /// <summary>
    /// 验证总预算取消当前回源并阻止后续批次启动。
    /// </summary>
    [Fact]
    public async Task RunAsync_BudgetCancellationStopsLaterBatches()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(o =>
        {
            o.MaxConcurrentFactories = 1;
            o.MaxQueuedFactories = 0;
        }, name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var factoryEntered = TestCache.Signal<bool>();
        var release = TestCache.Signal<bool>();
        var factoryCalls = 0;

        async Task<string> Load(string _, CancellationToken token)
        {
            Interlocked.Increment(ref factoryCalls);
            factoryEntered.TrySetResult(true);
            await release.Task.WaitAsync(token);
            return "loaded-value";
        }

        var run = SampleCacheWarmup.RunAsync(cache, ["first", "second"], Load,
            batchSize: 1, budget: TimeSpan.FromSeconds(1));
        await factoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, factoryCalls);
        Assert.True(result.TimedOut);
        Assert.False(result.CallerCancelled);
        Assert.Equal(1, result.Started);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(1, result.NotStarted);
        Assert.Equal(0, metrics.Active);
        AssertStatisticsConserved(result);
    }

    /// <summary>
    /// 验证调用前已取消时不会调用缓存或业务回源。
    /// </summary>
    [Fact]
    public async Task RunAsync_PreCancelledTokenDoesNotStartAnyKey()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var factoryCalls = 0;

        var result = await SampleCacheWarmup.RunAsync(cache, ["one", "two", "three"], (_, _) =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult("unexpected");
        }, batchSize: 2, budget: TimeSpan.FromSeconds(5), cancellationToken: cancellation.Token);

        Assert.Equal(0, factoryCalls);
        Assert.True(result.CallerCancelled);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.Started);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Cancelled);
        Assert.Equal(3, result.NotStarted);
        AssertStatisticsConserved(result);
    }

    /// <summary>
    /// 验证请求数、开始数和各终态统计之间的守恒关系。
    /// </summary>
    /// <param name="result">待检查的预热统计。</param>
    private static void AssertStatisticsConserved(SampleWarmupResult result)
    {
        Assert.Equal(result.Requested, result.Started + result.NotStarted);
        Assert.Equal(result.Started, result.Succeeded + result.Failed + result.Cancelled);
        Assert.Equal(result.Requested, result.NotStarted + result.Succeeded + result.Failed + result.Cancelled);
    }

    /// <summary>
    /// 以原子方式记录当前批次的并发峰值。
    /// </summary>
    /// <param name="peak">峰值记录。</param>
    /// <param name="value">当前活动回源数。</param>
    private static void UpdatePeak(ref int peak, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref peak);
            if (current >= value) return;
        }
        while (Interlocked.CompareExchange(ref peak, value, current) != current);
    }
}
