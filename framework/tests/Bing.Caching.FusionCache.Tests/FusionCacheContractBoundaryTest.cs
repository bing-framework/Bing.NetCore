using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存契约的边界语义。
/// </summary>
/// <remarks>
/// 覆盖命中与缺失区分、预取消令牌以及同一实例内的租户前缀隔离。
/// </remarks>
public class FusionCacheContractBoundaryTest
{
    /// <summary>
    /// 验证未命中与值类型默认值命中可通过 Hit 区分。
    /// </summary>
    [Fact]
    public async Task TryGet_DistinguishesMissingFromValueTypeDefault()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();

        var missing = await cache.TryGetAsync<int>("default-value");
        Assert.False(missing.Hit);
        Assert.Equal(0, missing.Value);

        await cache.SetAsync("default-value", 0);
        var hit = await cache.TryGetAsync<int>("default-value");
        Assert.True(hit.Hit);
        Assert.Equal(0, hit.Value);
    }

    /// <summary>
    /// 验证预取消令牌会在执行工厂或修改缓存之前取消所有操作。
    /// </summary>
    [Fact]
    public async Task PreCanceledToken_CancelsEveryOperationBeforeFactoryOrMutation()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("stable", 7, tags: ["tenant-a:group"]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var factoryCalls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.TryGetAsync<int>("stable", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrCreateAsync("new", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult(9);
        }, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SetAsync("new", 9, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.RemoveAsync("stable", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.ExpireAsync("stable", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.InvalidateTagAsync("tenant-a:group", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.ClearAsync(cancellation.Token));

        Assert.Equal(0, factoryCalls);
        Assert.False((await cache.TryGetAsync<int>("new")).Hit);
        Assert.Equal(7, (await cache.TryGetAsync<int>("stable")).Value);
    }

    /// <summary>
    /// 验证租户键前缀可在同一缓存实例内隔离数据键和标签失效。
    /// </summary>
    [Fact]
    public async Task TenantPrefixes_IsolateKeysAndTagsWithinOneInstance()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var tenantAKey = new CacheKey("catalog:item") { Prefix = "tenant-a:" };
        var tenantBKey = new CacheKey("catalog:item") { Prefix = "tenant-b:" };

        await cache.SetAsync(tenantAKey, 11, tags: ["tenant-a:catalog"]);
        await cache.SetAsync(tenantBKey, 22, tags: ["tenant-b:catalog"]);

        Assert.Equal(11, (await cache.TryGetAsync<int>(tenantAKey)).Value);
        Assert.Equal(22, (await cache.TryGetAsync<int>(tenantBKey)).Value);

        await cache.InvalidateTagAsync("tenant-a:catalog");

        Assert.False((await cache.TryGetAsync<int>(tenantAKey)).Hit);
        Assert.Equal(22, (await cache.TryGetAsync<int>(tenantBKey)).Value);
    }
}
