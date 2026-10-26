using Bing.Caching.FusionCache.Internal;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存门面的空值语义、选项复制、键编码和输入限制。
/// </summary>
public class FusionCacheManagerTest
{
    /// <summary>
    /// 验证 null 命中与缺失可区分，并保留调用方传入的逻辑键。
    /// </summary>
    [Fact]
    public async Task NullHit_IsDifferentFromMissing_AndPreservesLogicalKey()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        Assert.False((await cache.TryGetAsync<string>("missing")).Hit);
        await cache.SetAsync<string?>("产品:1/%", null);
        var result = await cache.TryGetAsync<string?>("产品:1/%");
        Assert.True(result.Hit);
        Assert.Null(result.Value);
        Assert.Equal("产品:1/%", result.Key);
        Assert.Null(await cache.GetOrCreateAsync<string?>("产品:1/%", _ => throw new Exception("must hit")));
        await cache.RemoveAsync("产品:1/%");
        Assert.False((await cache.TryGetAsync<string>("产品:1/%")).Hit);
    }

    /// <summary>
    /// 验证工厂返回的 null 会被缓存，而工厂异常不会被缓存。
    /// </summary>
    [Fact]
    public async Task NullFactory_IsCached_ButFailureIsNot()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.GetOrCreateAsync<string?>("null", _ => Task.FromResult<string?>(null));
        Assert.True((await cache.TryGetAsync<string?>("null")).Hit);
        await Assert.ThrowsAsync<FormatException>(() =>
            cache.GetOrCreateAsync<int>("error", _ => throw new FormatException()));
        Assert.False((await cache.TryGetAsync<int>("error")).Hit);
        Assert.Equal(42, await cache.GetOrCreateAsync("error", _ => Task.FromResult(42)));
    }

    /// <summary>
    /// 验证条目选项会被复制，且正数亚毫秒 TTL 向上取整为至少一毫秒。
    /// </summary>
    [Fact]
    public void Options_AreCopied_AndPositiveSubMillisecondTtlRoundsUp()
    {
        var defaults = new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(7) };
        var mapper = new FusionCacheEntryOptionsMapper(defaults);
        defaults.Duration = TimeSpan.FromHours(2);
        Assert.Equal(TimeSpan.FromMinutes(7), mapper.Map().Duration);
        var caller = new CacheOptions { Expiration = TimeSpan.FromTicks(1) };
        var mapped = mapper.Map(caller);
        Assert.Equal(TimeSpan.FromMilliseconds(1), mapped.Duration);
        mapped.Duration = TimeSpan.FromHours(1);
        Assert.Equal(TimeSpan.FromMinutes(7), mapper.Map().Duration);
        Assert.Equal(TimeSpan.FromTicks(1), caller.Expiration);
        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.Map(new CacheOptions { Expiration = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.Map(new CacheOptions { Expiration = TimeSpan.FromSeconds(-1) }));
    }

    /// <summary>
    /// 验证显式 TTL 会按时过期且不改变实例默认有效期。
    /// </summary>
    [Fact]
    public async Task ExplicitTtlExpires_WithoutChangingDefault()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("short", 1, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(30) });
        await cache.SetAsync("default", 2);
        await TestCache.Eventually(async () => !(await cache.TryGetAsync<int>("short")).Hit);
        Assert.Equal(2, (await cache.TryGetAsync<int>("default")).Value);
    }

    /// <summary>
    /// 验证同一逻辑键的缓存值类型不匹配时会报告转换异常。
    /// </summary>
    [Fact]
    public async Task TypeMismatch_IsReported()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("key", 42);
        await Assert.ThrowsAsync<InvalidCastException>(() => cache.TryGetAsync<string>("key"));
        await Assert.ThrowsAsync<InvalidCastException>(() => cache.GetOrCreateAsync("key", _ => Task.FromResult("wrong")));
    }

    /// <summary>
    /// 验证 CacheKey 扩展方法与其字符串键入口访问同一条缓存项。
    /// </summary>
    [Fact]
    public async Task CacheKeyExtensions_UseTheSameEntry()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var key = new CacheKey("product:{0}", 7) { Prefix = "tenant:" };
        await cache.SetAsync(key, 1);
        Assert.Equal(1, (await cache.TryGetAsync<int>("tenant:product:7")).Value);
        Assert.Equal(1, await cache.GetOrCreateAsync(key, _ => Task.FromResult(2)));
        await cache.ExpireAsync(key);
        Assert.False((await cache.TryGetAsync<int>(key)).Hit);
        await cache.SetAsync(key, 3);
        await cache.RemoveAsync(key);
        Assert.False((await cache.TryGetAsync<int>(key)).Hit);
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.TryGetAsync<int>((CacheKey)null!));
    }

    /// <summary>
    /// 验证空白逻辑键会在执行回源工厂前失败。
    /// </summary>
    /// <param name="key">待验证的空白键。</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidKeyFailsBeforeFactory(string key)
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.GetOrCreateAsync<int>(key, _ => throw new Exception()));
    }

    /// <summary>
    /// 验证未配对的 UTF-16 代理项会在编码前被拒绝。
    /// </summary>
    [Fact]
    public async Task UnpairedSurrogate_IsRejectedBeforeEncoding()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        var key = new string((char)0xd800, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetOrCreateAsync<int>(key, _ => throw new Exception()));
    }

    /// <summary>
    /// 验证超长键、超长标签和超量标签会在写入前被拒绝。
    /// </summary>
    [Fact]
    public async Task InputLimits_RejectOversizedKeysAndTagsWithoutWriting()
    {
        using var services = TestCache.Create(o => { o.MaxKeyLength = 8; o.MaxTagLength = 3; o.MaxTagsPerEntry = 1; });
        var cache = services.GetRequiredService<IHybridCache>();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.TryGetAsync<int>("123456789"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.SetAsync("key", 1, tags: new[] { "long" }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.SetAsync("key", 1, tags: new[] { "a", "b" }));
        Assert.False((await cache.TryGetAsync<int>("key")).Hit);
    }

    /// <summary>
    /// 验证键编码不会让不同逻辑键碰撞，也不会解释通配符或原生内部标记。
    /// </summary>
    [Fact]
    public async Task Encoding_DoesNotCollideOrInterpretWildcards()
    {
        using var services = TestCache.Create();
        var cache = services.GetRequiredService<IHybridCache>();
        string[] keys = ["a:b", "a%3Ab", "*", "{x}", "清单", "__fc:tag:t", "d:x"];
        for (var i = 0; i < keys.Length; i++) await cache.SetAsync(keys[i], i);
        for (var i = 0; i < keys.Length; i++) Assert.Equal(i, (await cache.TryGetAsync<int>(keys[i])).Value);
    }
}
