using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存的 Fail-Safe、显式失效和提前刷新在依赖注入作用域下的行为。
/// </summary>
public class FusionCacheResilienceTest
{
    /// <summary>
    /// 验证软过期允许 Fail-Safe 回退，而 TryGet 始终不返回过期旧值。
    /// </summary>
    [Fact]
    public async Task ExpireAllowsFailSafe_ButTryGetNeverReturnsStale()
    {
        using var services = TestCache.Create(native: b => b.WithDefaultEntryOptions(o =>
        {
            o.IsFailSafeEnabled = true;
            o.FailSafeMaxDuration = TimeSpan.FromMinutes(1);
        }));
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 7);
        await cache.ExpireAsync("key");
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
        Assert.Equal(7, await cache.GetOrCreateAsync<int>("key", _ => throw new FormatException()));
    }

    /// <summary>
    /// 验证标签失效、清除和移除操作都不会再使用旧值后备。
    /// </summary>
    /// <param name="operation">本次执行的失效操作。</param>
    [Theory]
    [InlineData("tag")]
    [InlineData("clear")]
    [InlineData("remove")]
    public async Task ExplicitInvalidationDisallowsFailSafe(string operation)
    {
        using var services = TestCache.Create(native: b => b.WithDefaultEntryOptions(o => o.IsFailSafeEnabled = true));
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 7, tags: ["group"]);
        switch (operation)
        {
            case "tag": await cache.InvalidateTagAsync("group"); break;
            case "clear": await cache.ClearAsync(); break;
            case "remove": await cache.RemoveAsync("key"); break;
        }
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
        await Assert.ThrowsAsync<FormatException>(() => cache.GetOrCreateAsync<int>("key", _ => throw new FormatException()));
    }

    /// <summary>
    /// 验证提前刷新使用独立作用域，并占用回源工厂额度直到后台完成。
    /// </summary>
    [Fact]
    public async Task EagerRefreshUsesIndependentScope_AndConsumesFactoryBudget()
    {
        var name = Guid.NewGuid().ToString("N");
        using var metrics = new FactoryMetrics(name);
        var services = new ServiceCollection();
        services.AddScoped<ScopedReader>();
        services.AddBingFusionCache(name, o => { o.Namespace = "scope-test"; o.UseAsDefault = true; },
            b => b.WithDefaultEntryOptions(o =>
            {
                o.Duration = TimeSpan.FromSeconds(2);
                o.EagerRefreshThreshold = 0.01f;
            }));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var cache = provider.GetRequiredService<IHybridCache>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        await cache.SetAsync("key", 1);
        await Task.Delay(50);
        var started = TestCache.Signal<ScopedReader>();
        var release = TestCache.Signal<bool>();
        var finished = TestCache.Signal<bool>();
        var request = provider.CreateAsyncScope();
        var requestReader = request.ServiceProvider.GetRequiredService<ScopedReader>();
        var result = await cache.GetOrCreateAsync("key", async token =>
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var reader = scope.ServiceProvider.GetRequiredService<ScopedReader>();
                started.TrySetResult(reader);
                await release.Task;
                Assert.False(reader.Disposed);
                Assert.NotSame(requestReader, reader);
            }
            finished.TrySetResult(true);
            return 2;
        });
        Assert.Equal(1, result);
        var backgroundReader = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await request.DisposeAsync();
            Assert.True(requestReader.Disposed);
            Assert.False(backgroundReader.Disposed);
            Assert.Equal(1, metrics.Active);
        }
        finally { release.TrySetResult(true); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await TestCache.Eventually(async () => (await cache.TryGetAsync<int>("key")).Value == 2);
        Assert.True(backgroundReader.Disposed);
        Assert.Equal(0, metrics.Active);
    }

    /// <summary>
    /// 记录自身释放状态的测试作用域依赖。
    /// </summary>
    private sealed class ScopedReader : IDisposable
    {
        /// <summary>
        /// 获取当前实例是否已释放。
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// 标记当前实例已释放。
        /// </summary>
        public void Dispose() => Disposed = true;
    }
}
