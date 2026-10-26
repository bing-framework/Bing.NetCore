using Microsoft.Extensions.DependencyInjection;
using Moq;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存对原生 FusionCache 软超时、Fail-Safe、取消和 Backplane 策略的转发行为。
/// </summary>
public class FusionCachePolicyTest
{
    /// <summary>
    /// 验证软超时返回旧值，且后台完成的工厂继续持有回源额度。
    /// </summary>
    [Fact]
    public async Task SoftTimeoutReturnsFallback_AndBackgroundCompletionKeepsPermit()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        using var services = TestCache.Create(native: b => b.WithDefaultEntryOptions(o =>
        {
            o.IsFailSafeEnabled = true;
            o.FactorySoftTimeout = TimeSpan.FromMilliseconds(50);
            o.AllowTimedOutFactoryBackgroundCompletion = true;
        }), name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 1);
        await cache.ExpireAsync("key");
        var release = TestCache.Signal<int>();
        try
        {
            Assert.Equal(1, await cache.GetOrCreateAsync("key", _ => release.Task).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, metrics.Active);
        }
        finally { release.TrySetResult(2); }
        await TestCache.Eventually(async () => (await cache.TryGetAsync<int>("key")).Value == 2);
        Assert.Equal(0, metrics.Active);
    }

    /// <summary>
    /// 验证没有旧值可回退时，软超时不会提前返回而是等待工厂完成。
    /// </summary>
    [Fact]
    public async Task SoftTimeoutWithoutFallbackWaitsForFactory()
    {
        using var services = TestCache.Create(native: b => b.WithDefaultEntryOptions(o =>
        {
            o.IsFailSafeEnabled = true;
            o.FactorySoftTimeout = TimeSpan.FromMilliseconds(20);
        }));
        var cache = services.GetRequiredService<IHybridCache>();
        var release = TestCache.Signal<int>();
        var started = TestCache.Signal<bool>();
        var task = cache.GetOrCreateAsync("cold", _ => { started.TrySetResult(true); return release.Task; });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(60);
            Assert.False(task.IsCompleted);
        }
        finally { release.TrySetResult(9); }
        Assert.Equal(9, await task);
    }

    /// <summary>
    /// 验证 Fail-Safe 旧值最终会超过最大保留时间并停止回退。
    /// </summary>
    [Fact]
    public async Task FailSafeRetentionEventuallyExpires()
    {
        using var services = TestCache.Create(native: b => b.WithDefaultEntryOptions(o =>
        {
            o.Duration = TimeSpan.FromMilliseconds(20);
            o.IsFailSafeEnabled = true;
            o.FailSafeMaxDuration = TimeSpan.FromMilliseconds(80);
        }));
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 1);
        await Task.Delay(150);
        await Assert.ThrowsAsync<FormatException>(() => cache.GetOrCreateAsync<int>("key", _ => throw new FormatException()));
    }

    /// <summary>
    /// 验证调用方取消会传递到正在执行的回源工厂。
    /// </summary>
    [Fact]
    public async Task CallerCancellationReachesActiveFactory()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        using var cancellation = new CancellationTokenSource();
        var started = TestCache.Signal<bool>();
        var task = cache.GetOrCreateAsync("key", async token =>
        {
            started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
    }

    /// <summary>
    /// 验证可变值默认共享引用，启用自动克隆后每次读取返回独立实例。
    /// </summary>
    [Fact]
    public async Task MutableValuesShareReferences_UnlessAutoCloneIsEnabled()
    {
        foreach (var clone in new[] { false, true })
        {
            using var services = TestCache.Create(native: b => b
                .WithSerializer(new FusionCacheSystemTextJsonSerializer())
                .WithDefaultEntryOptions(o => o.EnableAutoClone = clone));
            var cache = services.GetRequiredService<IHybridCache>();
            await cache.SetAsync("dto", new MutableDto { Value = 1 });
            var first = (await cache.TryGetAsync<MutableDto>("dto")).Value;
            first.Value = 2;
            var second = (await cache.TryGetAsync<MutableDto>("dto")).Value;
            Assert.Equal(clone ? 1 : 2, second.Value);
            Assert.Equal(!clone, ReferenceEquals(first, second));
        }
    }

    /// <summary>
    /// 验证只读策略在门面解析后保持独立快照，并发读取仍克隆新鲜值且不返回过期值。
    /// </summary>
    [Fact]
    public async Task TryGetKeepsFrozenReadPolicy_AutoClonesFreshValuesAndRejectsExpiredStaleValue()
    {
        var name = Guid.NewGuid().ToString("N");
        using var services = TestCache.Create(native: b => b
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDefaultEntryOptions(o =>
            {
                o.EnableAutoClone = true;
                o.IsFailSafeEnabled = true;
                o.FailSafeMaxDuration = TimeSpan.FromMinutes(1);
            }), name: name);
        var cache = services.GetRequiredService<IHybridCache>();
        var native = services.GetRequiredService<IFusionCacheProvider>().GetCache(name);
        await cache.SetAsync("dto", new MutableDto { Value = 1 });

        native.DefaultEntryOptions.EnableAutoClone = false;
        native.DefaultEntryOptions.AllowStaleOnReadOnly = true;
        native.DefaultEntryOptions.SkipMemoryCacheRead = true;

        var first = await cache.TryGetAsync<MutableDto>("dto");
        Assert.True(first.Hit);
        Assert.Equal(1, first.Value.Value);
        first.Value.Value = 2;

        var second = await cache.TryGetAsync<MutableDto>("dto");
        Assert.True(second.Hit);
        Assert.Equal(1, second.Value.Value);
        Assert.NotSame(first.Value, second.Value);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => cache.TryGetAsync<MutableDto>("dto"))));
        Assert.All(concurrent, entry =>
        {
            Assert.True(entry.Hit);
            Assert.Equal(1, entry.Value.Value);
            Assert.NotSame(first.Value, entry.Value);
        });
        for (var i = 0; i < concurrent.Length; i++)
            for (var j = i + 1; j < concurrent.Length; j++)
                Assert.NotSame(concurrent[i].Value, concurrent[j].Value);

        await cache.ExpireAsync("dto");
        var expired = await cache.TryGetAsync<MutableDto>("dto");
        Assert.False(expired.Hit);
    }

    /// <summary>
    /// 验证前台 Backplane 发布会等待完成，且发布失败在本地写入成功后仍会报告。
    /// </summary>
    [Fact]
    public async Task ForegroundBackplaneIsAwaited_AndFailureIsReportedAfterLocalWrite()
    {
        var release = TestCache.Signal<bool>();
        var invoked = TestCache.Signal<bool>();
        var backplane = new Mock<IFusionCacheBackplane>();
        backplane.Setup(x => x.PublishAsync(It.IsAny<BackplaneMessage>(), It.IsAny<FusionCacheEntryOptions>(),
            It.IsAny<CancellationToken>())).Returns(() => new ValueTask(Publish()));
        async Task Publish()
        {
            invoked.TrySetResult(true);
            await release.Task;
            throw new InvalidOperationException("Injected backplane failure.");
        }
        using var services = TestCache.Create(native: b => b.WithBackplane(backplane.Object));
        var cache = services.GetRequiredService<IHybridCache>();
        var write = cache.SetAsync("key", 1);
        try
        {
            await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(write.IsCompleted);
        }
        finally { release.TrySetResult(true); }
        await Assert.ThrowsAnyAsync<Exception>(() => write);
        Assert.Equal(1, (await cache.TryGetAsync<int>("key")).Value);
    }

    /// <summary>
    /// 可修改的测试 DTO，用于验证自动克隆是否隔离缓存读取结果。
    /// </summary>
    public sealed class MutableDto
    {
        /// <summary>
        /// 获取或设置测试值。
        /// </summary>
        public int Value { get; set; }
    }
}
