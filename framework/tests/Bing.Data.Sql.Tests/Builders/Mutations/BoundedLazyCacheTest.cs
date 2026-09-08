using System.Collections.Concurrent;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// 有界惰性缓存的直接职责测试。
/// </summary>
public sealed class BoundedLazyCacheTest
{
    /// <summary>
    /// 测试目的：无上限缓存应复用同一个 Lazy，并分别报告命中和未命中。
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenUnboundedKeyIsRepeated_ShouldReuseLazyAndReportHit()
    {
        var cache = new BoundedLazyCache<string, int>();
        var factoryCalls = 0;

        var first = cache.GetOrAdd("key", () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return new Lazy<int>(() => 42, LazyThreadSafetyMode.ExecutionAndPublication);
        });
        var second = cache.GetOrAdd("key", () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return new Lazy<int>(() => 7, LazyThreadSafetyMode.ExecutionAndPublication);
        });

        Assert.Same(first, second);
        Assert.Equal(42, second.Value);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, cache.HitCount);
        Assert.Equal(1, cache.MissCount);
        Assert.Equal(0, cache.BypassCount);
        Assert.Equal(0, cache.EvictionCount);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// 测试目的：零容量缓存应每次旁路并返回独立 Lazy。
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenCapacityIsZero_ShouldBypassCache()
    {
        var cache = new BoundedLazyCache<string, int>(0);

        var first = cache.GetOrAdd("key", () => new Lazy<int>(() => 1));
        var second = cache.GetOrAdd("key", () => new Lazy<int>(() => 2));

        Assert.NotSame(first, second);
        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.HitCount);
        Assert.Equal(2, cache.MissCount);
        Assert.Equal(2, cache.BypassCount);
        Assert.Equal(0, cache.EvictionCount);
    }

    /// <summary>
    /// 测试目的：正容量缓存应在触碰最近项后淘汰最久未使用项。
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenCapacityIsReached_ShouldEvictLeastRecentlyUsedEntry()
    {
        var cache = new BoundedLazyCache<string, int>(2);
        var firstA = cache.GetOrAdd("a", () => new Lazy<int>(() => 1));
        var firstB = cache.GetOrAdd("b", () => new Lazy<int>(() => 2));

        var recentA = cache.GetOrAdd("a", () => throw new InvalidOperationException("a should be cached"));
        var firstC = cache.GetOrAdd("c", () => new Lazy<int>(() => 3));
        var rebuiltB = cache.GetOrAdd("b", () => new Lazy<int>(() => 4));

        Assert.Same(firstA, recentA);
        Assert.NotSame(firstB, rebuiltB);
        Assert.Equal(3, firstC.Value);
        Assert.Equal(4, rebuiltB.Value);
        Assert.Equal(2, cache.Count);
        Assert.Equal(1, cache.HitCount);
        Assert.Equal(4, cache.MissCount);
        Assert.Equal(0, cache.BypassCount);
        Assert.Equal(2, cache.EvictionCount);
    }

    /// <summary>
    /// 测试目的：RemoveIfCurrent 只能删除当前 Lazy，不能删除已被替换的条目。
    /// </summary>
    [Fact]
    public void RemoveIfCurrent_WhenLazyIsStale_ShouldKeepCurrentEntry()
    {
        var cache = new BoundedLazyCache<string, int>();
        var current = cache.GetOrAdd("key", () => new Lazy<int>(() => 1));
        var stale = new Lazy<int>(() => 2);

        cache.RemoveIfCurrent("key", stale);
        var stillCurrent = cache.GetOrAdd("key", () => throw new InvalidOperationException("stale removal occurred"));
        cache.RemoveIfCurrent("key", current);
        var rebuilt = cache.GetOrAdd("key", () => new Lazy<int>(() => 3));

        Assert.Same(current, stillCurrent);
        Assert.NotSame(current, rebuilt);
        Assert.Equal(3, rebuilt.Value);
        Assert.Equal(1, cache.HitCount);
        Assert.Equal(2, cache.MissCount);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// 测试目的：同一键并发首次读取时，工厂只能发布一个 Lazy。
    /// </summary>
    [Fact]
    public async Task GetOrAdd_WhenSameKeyIsReadConcurrently_ShouldPublishOneLazy()
    {
        var cache = new BoundedLazyCache<string, int>();
        var factoryCalls = 0;
        var values = new ConcurrentBag<Lazy<int>>();
        using var ready = new CountdownEvent(32);
        using var start = new ManualResetEventSlim();

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();
            values.Add(cache.GetOrAdd("key", () =>
            {
                Interlocked.Increment(ref factoryCalls);
                return new Lazy<int>(() => 42, LazyThreadSafetyMode.ExecutionAndPublication);
            }));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        Assert.True(SpinWait.SpinUntil(() => ready.CurrentCount == 0, TimeSpan.FromSeconds(5)));
        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, factoryCalls);
        Assert.Equal(32, values.Count);
        Assert.All(values, value => Assert.Same(values.First(), value));
        Assert.Equal(42, values.First().Value);
        Assert.Equal(31, cache.HitCount);
        Assert.Equal(1, cache.MissCount);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// 测试目的：负容量应在缓存创建阶段被拒绝。
    /// </summary>
    [Fact]
    public void Constructor_WhenCapacityIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLazyCache<string, int>(-1));

        Assert.Equal("capacity", exception.ParamName);
    }
}
