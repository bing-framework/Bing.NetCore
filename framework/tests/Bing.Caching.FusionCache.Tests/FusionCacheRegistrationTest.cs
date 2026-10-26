using Bing.Caching.FusionCache.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存的注册校验、默认实例规则和命名隔离。
/// </summary>
public class FusionCacheRegistrationTest
{
    /// <summary>
    /// 验证命名实例不会隐式成为默认实例，未注册名称会立即失败。
    /// </summary>
    [Fact]
    public void NamedCache_HasNoImplicitDefault_AndMissingNameFails()
    {
        var services = new ServiceCollection();
        services.AddBingFusionCache("one", o => o.Namespace = "test");
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IHybridCache>());
        var caches = provider.GetRequiredService<IHybridCacheProvider>();
        Assert.Same(caches.GetCache("one"), caches.GetCache("one"));
        Assert.Equal("one", caches.GetCache("one").Name);
        Assert.Throws<InvalidOperationException>(() => caches.GetCache("missing"));
    }

    /// <summary>
    /// 验证重复名称和重复默认实例会在注册阶段失败。
    /// </summary>
    [Fact]
    public void DuplicateNamesAndDefaultsFailAtRegistration()
    {
        var services = new ServiceCollection();
        services.AddBingFusionCache("one", o => { o.Namespace = "test"; o.UseAsDefault = true; });
        Assert.Throws<InvalidOperationException>(() => services.AddBingFusionCache("one", o => o.Namespace = "other"));
        Assert.Throws<InvalidOperationException>(() => services.AddBingFusionCache("two",
            o => { o.Namespace = "test"; o.UseAsDefault = true; }));
    }

    /// <summary>
    /// 验证缺少命名空间或回源预算无效时注册失败。
    /// </summary>
    [Fact]
    public void MissingNamespaceAndInvalidBudgetsFail()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingFusionCache("one", _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestCache.Create(o => o.MaxConcurrentFactories = 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestCache.Create(o => o.FactoryQueueTimeout = TimeSpan.Zero));
    }

    /// <summary>
    /// 验证缓存身份、前缀、标签和自动恢复契约不能被原生配置覆盖。
    /// </summary>
    /// <param name="property">本次尝试覆盖的配置项。</param>
    [Theory]
    [InlineData("name")]
    [InlineData("prefix")]
    [InlineData("builder-prefix")]
    [InlineData("channel")]
    [InlineData("tags")]
    [InlineData("remove")]
    [InlineData("unbounded")]
    public void IdentityAndInvalidationContractsCannotBeOverridden(string property)
    {
        using var provider = TestCache.Create(native: b =>
        {
            if (property == "builder-prefix") b.WithCacheKeyPrefix("bad");
            else b.WithOptions(o =>
            {
                switch (property)
                {
                    case "name": o.CacheName = "bad"; break;
                    case "prefix": o.CacheKeyPrefix = "bad"; break;
                    case "channel": o.BackplaneChannelPrefix = "bad"; break;
                    case "tags": o.DisableTagging = true; break;
                    case "remove": o.RemoveByTagBehavior = RemoveByTagBehavior.Expire; break;
                    case "unbounded": o.AutoRecoveryMaxItems = null; break;
                }
            });
        });
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IHybridCache>());
    }

    /// <summary>
    /// 验证注册时复制配置，且原生默认值与 Bing 的保守策略一致。
    /// </summary>
    [Fact]
    public async Task RegisteredOptionsAreSnapshotted_AndDefaultsAreConservative()
    {
        BingFusionCacheOptions? captured = null;
        using var provider = TestCache.Create(o => captured = o);
        captured!.MaxKeyLength = 1;
        var cache = provider.GetRequiredService<IHybridCache>();
        var native = provider.GetRequiredService<IFusionCacheProvider>().GetCache("catalog");
        var options = native.DefaultEntryOptions;
        Assert.Equal(TimeSpan.FromMinutes(5), options.Duration);
        Assert.Equal(TimeSpan.FromMinutes(2), options.FactoryHardTimeout);
        Assert.False(options.IsFailSafeEnabled);
        Assert.Null(options.EagerRefreshThreshold);
        Assert.False(options.AllowTimedOutFactoryBackgroundCompletion);
        Assert.False(options.AllowBackgroundDistributedCacheOperations);
        Assert.False(options.AllowBackgroundBackplaneOperations);
        Assert.True(options.ReThrowDistributedCacheExceptions);
        Assert.True(options.ReThrowBackplaneExceptions);
        Assert.True(options.ReThrowSerializationExceptions);
        // 配置调用结束后修改原始配置不能改变已注册实例。
        await cache.SetAsync("longer", 1);
    }

    /// <summary>
    /// 验证宿主启动阶段会解析全部命名实例，缺少 L2 时立即失败。
    /// </summary>
    [Fact]
    public void HostStartupResolvesNamedCaches_AndMissingL2Fails()
    {
        using var provider = TestCache.Create(native: b => b.WithRegisteredDistributedCache());
        Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>().ToArray());
    }

    /// <summary>
    /// 验证不同命名实例会隔离数据、标签和清除操作。
    /// </summary>
    [Fact]
    public async Task NamedInstances_IsolateDataTagsAndClear()
    {
        var services = new ServiceCollection();
        services.AddBingFusionCache("a:b", o => o.Namespace = "app");
        services.AddBingFusionCache("a%3Ab", o => o.Namespace = "app");
        using var provider = services.BuildServiceProvider();
        var caches = provider.GetRequiredService<IHybridCacheProvider>();
        var a = caches.GetCache("a:b");
        var b = caches.GetCache("a%3Ab");
        await a.SetAsync("key", 1, tags: ["group"]);
        await b.SetAsync("key", 2, tags: ["group"]);
        await a.InvalidateTagAsync("group");
        Assert.False((await a.TryGetAsync<int>("key")).Hit);
        Assert.Equal(2, (await b.TryGetAsync<int>("key")).Value);
        await a.ClearAsync();
        Assert.Equal(2, (await b.TryGetAsync<int>("key")).Value);
        Assert.NotEqual(FusionCacheKeyNormalizer.Prefix("app:a", "b"),
            FusionCacheKeyNormalizer.Prefix("app", "a:b"));
    }
}
