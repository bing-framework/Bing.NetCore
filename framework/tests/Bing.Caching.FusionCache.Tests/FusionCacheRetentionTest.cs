using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证数据条目保留窗口不能超过标签与清除标记的物理保留窗口。
/// </summary>
public class FusionCacheRetentionTest
{
    /// <summary>
    /// 验证 TTL、内存层、Fail-Safe 或抖动导致的数据窗口超出标记窗口时解析失败。
    /// </summary>
    /// <param name="kind">本次放大的保留期类型。</param>
    [Theory]
    [InlineData("ttl")]
    [InlineData("memory")]
    [InlineData("failsafe")]
    [InlineData("jitter")]
    public void UnsafePhysicalWindowFailsAtResolution(string kind)
    {
        using var provider = TestCache.Create(native: b => b.WithOptions(o =>
        {
            o.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(1);
            o.TagsDefaultEntryOptions.Duration = TimeSpan.FromSeconds(2);
            o.TagsDefaultEntryOptions.IsFailSafeEnabled = false;
            switch (kind)
            {
                case "ttl": o.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(20); break;
                case "memory": o.DefaultEntryOptions.MemoryCacheDuration = TimeSpan.FromSeconds(20); break;
                case "failsafe": o.DefaultEntryOptions.IsFailSafeEnabled = true; o.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromSeconds(20); break;
                case "jitter": o.DefaultEntryOptions.JitterMaxDuration = TimeSpan.FromSeconds(20); break;
            }
        }));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IHybridCache>());
    }

    /// <summary>
    /// 验证单次调用 TTL 不能超过标记窗口，且被拒绝时不会执行回源工厂。
    /// </summary>
    [Fact]
    public async Task PerCallTtlCannotOutliveMarkers_AndDoesNotExecuteFactory()
    {
        using var provider = TestCache.Create();
        var cache = provider.GetRequiredService<IHybridCache>();
        var options = new CacheOptions { Expiration = TimeSpan.FromDays(30) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync("key", 1, options));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync<int>("key", _ => throw new Exception("must not run"), options));
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
    }

    /// <summary>
    /// 验证长物理数据标记在逻辑过期后仍能阻止标签失效或清除后的旧值复活。
    /// </summary>
    /// <param name="clear">为 <see langword="true" /> 时使用清除操作；否则使用标签失效。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongPhysicalMarkersPreventResurrection_AfterLogicalExpiry(bool clear)
    {
        using var provider = TestCache.Create(native: b => b.WithOptions(o =>
        {
            o.DefaultEntryOptions.Duration = TimeSpan.FromSeconds(20);
            o.DefaultEntryOptions.IsFailSafeEnabled = true;
            o.DefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromSeconds(30);
            o.TagsDefaultEntryOptions.Duration = TimeSpan.FromMilliseconds(30);
            o.TagsDefaultEntryOptions.FailSafeMaxDuration = TimeSpan.FromMinutes(1);
        }));
        var cache = provider.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 42, tags: ["tag"]);
        if (clear) await cache.ClearAsync(); else await cache.InvalidateTagAsync("tag");
        // 期间不读条目，避免惰性清理掩盖标记丢失导致的复活。
        await Task.Delay(100);
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
        Assert.Equal(7, await cache.GetOrCreateAsync("key", _ => Task.FromResult(7)));
    }
}
