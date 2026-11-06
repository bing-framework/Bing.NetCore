using Bing.Samples.FusionCache;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存中常见业务流程的可观察行为。
/// </summary>
public class FusionCacheCommonScenarioTest
{
    /// <summary>
    /// 验证数据库提交后移除缓存可读取新版本，且后续读取命中缓存。
    /// </summary>
    [Fact]
    public async Task RemoveAfterDatabaseCommit_LoadsNewVersionThenHitsCache()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var databaseVersion = 1;
        var factoryCalls = 0;

        Task<int> ReadDatabase(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult(databaseVersion);
        }

        (await cache.GetOrCreateAsync("product:42", ReadDatabase)).ShouldBe(1);
        databaseVersion = 2;

        await cache.RemoveAsync("product:42");

        (await cache.GetOrCreateAsync("product:42", ReadDatabase)).ShouldBe(2);
        (await cache.GetOrCreateAsync("product:42", ReadDatabase)).ShouldBe(2);
        factoryCalls.ShouldBe(2);
    }

    /// <summary>
    /// 验证 null 结果会负缓存，实体创建并失效后可重新回源取得。
    /// </summary>
    [Fact]
    public async Task NullFactoryResult_IsNegativeCachedUntilInvalidated()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        CatalogItem? databaseItem = null;
        var factoryCalls = 0;

        Task<CatalogItem?> FindItem(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult(databaseItem);
        }

        (await cache.GetOrCreateAsync<CatalogItem?>("catalog:item:42", FindItem)).ShouldBeNull();
        (await cache.GetOrCreateAsync<CatalogItem?>("catalog:item:42", FindItem)).ShouldBeNull();
        factoryCalls.ShouldBe(1);

        databaseItem = new CatalogItem(42, "Keyboard");
        await cache.RemoveAsync("catalog:item:42");

        var created = await cache.GetOrCreateAsync<CatalogItem?>("catalog:item:42", FindItem);
        created.ShouldBe(databaseItem);
        (await cache.GetOrCreateAsync<CatalogItem?>("catalog:item:42", FindItem)).ShouldBe(databaseItem);
        factoryCalls.ShouldBe(2);
    }

    /// <summary>
    /// 验证同一租户分类标签可批量失效条目，同时保留其他分类与租户的数据。
    /// </summary>
    [Fact]
    public async Task InvalidateTenantCategory_RemovesOnlyMatchingItems()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        const string tenantACategory = "tenant-a:category:electronics";
        await cache.SetAsync("tenant-a:product:phone", "Phone", tags: [tenantACategory]);
        await cache.SetAsync("tenant-a:product:tablet", "Tablet", tags: [tenantACategory]);
        await cache.SetAsync("tenant-a:product:book", "Book", tags: ["tenant-a:category:books"]);
        await cache.SetAsync("tenant-b:product:phone", "Phone B", tags: ["tenant-b:category:electronics"]);

        await cache.InvalidateTagAsync(tenantACategory);

        (await cache.TryGetAsync<string>("tenant-a:product:phone")).Hit.ShouldBeFalse();
        (await cache.TryGetAsync<string>("tenant-a:product:tablet")).Hit.ShouldBeFalse();
        (await cache.TryGetAsync<string>("tenant-a:product:book")).Hit.ShouldBeTrue();
        (await cache.TryGetAsync<string>("tenant-b:product:phone")).Hit.ShouldBeTrue();
    }

    /// <summary>
    /// 验证多标签中的任一标签均可使条目失效，且重新填充后新标签仍可失效。
    /// </summary>
    [Fact]
    public async Task AnyAssociatedTagInvalidates_RefillUsesNewTags()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        const string key = "product:42";
        const string oldCategory = "catalog:hardware";

        await cache.SetAsync(key, "first", tags: ["product:42", oldCategory]);
        await cache.InvalidateTagAsync(oldCategory);
        (await cache.TryGetAsync<string>(key)).Hit.ShouldBeFalse();

        await cache.SetAsync(key, "second", tags: ["product:42", oldCategory]);
        await cache.InvalidateTagAsync("product:42");
        (await cache.TryGetAsync<string>(key)).Hit.ShouldBeFalse();

        await cache.SetAsync(key, "refilled", tags: ["product:42", "catalog:hardware-v2"]);
        await cache.InvalidateTagAsync(oldCategory);
        var refilled = await cache.TryGetAsync<string>(key);
        refilled.Hit.ShouldBeTrue();
        refilled.Value.ShouldBe("refilled");

        await cache.InvalidateTagAsync("catalog:hardware-v2");
        (await cache.TryGetAsync<string>(key)).Hit.ShouldBeFalse();
    }

    /// <summary>
    /// 验证取消同键等待者不会取消主工厂，其他等待者仍取得其结果。
    /// </summary>
    [Fact]
    public async Task CancellingSameKeyWaiter_DoesNotCancelOwnerOrOtherWaiter()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var ownerStarted = TestCache.Signal<bool>();
        var releaseOwner = TestCache.Signal<int>();
        var factoryCalls = 0;
        var waiterFactoryCalls = 0;
        var ownerFactoryToken = default(CancellationToken);

        var owner = cache.GetOrCreateAsync<int>("shared:record", token =>
        {
            Interlocked.Increment(ref factoryCalls);
            ownerFactoryToken = token;
            ownerStarted.TrySetResult(true);
            return releaseOwner.Task;
        });

        try
        {
            await ownerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var waiterCancellation = new CancellationTokenSource();
            var cancelledWaiter = cache.GetOrCreateAsync<int>("shared:record", _ =>
            {
                Interlocked.Increment(ref waiterFactoryCalls);
                return Task.FromResult(-1);
            }, cancellationToken: waiterCancellation.Token);

            waiterCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter.WaitAsync(TimeSpan.FromSeconds(5)));
            ownerFactoryToken.IsCancellationRequested.ShouldBeFalse();

            var remainingWaiter = cache.GetOrCreateAsync<int>("shared:record", _ =>
            {
                Interlocked.Increment(ref waiterFactoryCalls);
                return Task.FromResult(-2);
            });
            remainingWaiter.IsCompleted.ShouldBeFalse();

            releaseOwner.TrySetResult(42);
            (await owner.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(42);
            (await remainingWaiter.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(42);
            factoryCalls.ShouldBe(1);
            waiterFactoryCalls.ShouldBe(0);
        }
        finally
        {
            releaseOwner.TrySetResult(42);
            await owner.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// 验证不同命名缓存拥有独立工厂额度，实例 A 的阻塞不影响实例 B。
    /// </summary>
    [Fact]
    public async Task NamedInstances_HaveIndependentFactoryBudgets()
    {
        var services = new ServiceCollection();
        var cacheNamespace = "test:" + Guid.NewGuid().ToString("N");
        const string cacheAName = "catalog-a";
        const string cacheBName = "catalog-b";
        services.AddBingFusionCache(cacheAName, options =>
        {
            options.Namespace = cacheNamespace;
            options.MaxConcurrentFactories = 1;
            options.MaxQueuedFactories = 0;
        });
        services.AddBingFusionCache(cacheBName, options =>
        {
            options.Namespace = cacheNamespace;
            options.MaxConcurrentFactories = 1;
            options.MaxQueuedFactories = 0;
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var cacheProvider = provider.GetRequiredService<IHybridCacheProvider>();
        var cacheA = cacheProvider.GetCache(cacheAName);
        var cacheB = cacheProvider.GetCache(cacheBName);
        var factoryAStarted = TestCache.Signal<bool>();
        var releaseFactoryA = TestCache.Signal<int>();

        var blockedA = cacheA.GetOrCreateAsync<int>("blocked", _ =>
        {
            factoryAStarted.TrySetResult(true);
            return releaseFactoryA.Task;
        });

        try
        {
            await factoryAStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cacheA.GetOrCreateAsync<int>("another-key", _ => Task.FromResult(11)));

            (await cacheB.GetOrCreateAsync("independent", _ => Task.FromResult(22))
                .WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(22);
            blockedA.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseFactoryA.TrySetResult(10);
            await blockedA.WaitAsync(TimeSpan.FromSeconds(5));
        }

        (await blockedA.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(10);
    }

    /// <summary>
    /// 验证预热与在线同键读取重叠时只执行一次真实回源工厂。
    /// </summary>
    [Fact]
    public async Task WarmupAndOnlineRead_CoalesceIntoSingleFactory()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var warmupFactoryStarted = TestCache.Signal<bool>();
        var releaseWarmupFactory = TestCache.Signal<string>();
        var factoryCalls = 0;
        var onlineFactoryCalls = 0;

        Task<string> Load(string _, CancellationToken __)
        {
            Interlocked.Increment(ref factoryCalls);
            warmupFactoryStarted.TrySetResult(true);
            return releaseWarmupFactory.Task;
        }

        var warmup = SampleCacheWarmup.RunAsync(cache, ["product:42"], Load,
            batchSize: 1, budget: TimeSpan.FromSeconds(10));

        try
        {
            await warmupFactoryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var onlineRead = cache.GetOrCreateAsync("product:42", _ =>
            {
                Interlocked.Increment(ref onlineFactoryCalls);
                return Task.FromResult("unexpected-online-value");
            });
            onlineRead.IsCompleted.ShouldBeFalse();

            releaseWarmupFactory.TrySetResult("database-value");
            (await onlineRead.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe("database-value");
            var result = await warmup.WaitAsync(TimeSpan.FromSeconds(5));
            result.Requested.ShouldBe(1);
            result.Started.ShouldBe(1);
            result.Succeeded.ShouldBe(1);
            result.Failed.ShouldBe(0);
            result.Cancelled.ShouldBe(0);
            factoryCalls.ShouldBe(1);
            onlineFactoryCalls.ShouldBe(0);
        }
        finally
        {
            releaseWarmupFactory.TrySetResult("database-value");
            await warmup.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// 模拟数据源返回的不可变商品信息。
    /// </summary>
    private sealed record CatalogItem(int Id, string Name);
}
