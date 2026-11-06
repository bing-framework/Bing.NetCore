using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证不会静默接受门面无法执行的原生按键默认选项策略。
/// </summary>
public class FusionCacheDefaultOptionsProviderTest
{
    /// <summary>
    /// 最终原生配置包含按键策略时，默认和命名实例均在宿主启动阶段失败。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostStartupRejectsDefaultOptionsProvider(bool useAsDefault)
    {
        var services = new ServiceCollection();
        services.AddBingFusionCache("provider", o =>
        {
            o.Namespace = "provider-test";
            o.UseAsDefault = useAsDefault;
        }, builder => builder.WithOptions(o => o.DefaultEntryOptionsProvider = Mock.Of<FusionCacheEntryOptionsProvider>()));
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>().ToArray());
        Assert.Contains("DefaultEntryOptionsProvider", error.Message);
    }

    /// <summary>
    /// 校验以全部用户回调执行后的最终配置为准，允许先设置后移除策略。
    /// </summary>
    [Fact]
    public async Task RemovedProviderIsAccepted_AndStaticDefaultsRemainUsable()
    {
        using var provider = TestCache.Create(native: builder => builder
            .WithOptions(o => o.DefaultEntryOptionsProvider = Mock.Of<FusionCacheEntryOptionsProvider>())
            .WithOptions(o => o.DefaultEntryOptionsProvider = null)
            .WithDefaultEntryOptions(o => o.Duration = TimeSpan.FromMinutes(10)));
        var cache = provider.GetRequiredService<IHybridCache>();
        var native = provider.GetRequiredService<IFusionCacheProvider>().GetCache("catalog");
        Assert.Equal(TimeSpan.FromMinutes(10), native.DefaultEntryOptions.Duration);

        await cache.SetAsync("default", 1);
        await cache.SetAsync("short", 2, new CacheOptions { Expiration = TimeSpan.FromMilliseconds(30) });
        await TestCache.Eventually(async () => !(await cache.TryGetAsync<int>("short")).Hit);
        Assert.Equal(1, (await cache.TryGetAsync<int>("default")).Value);
        Assert.Equal(TimeSpan.FromMinutes(10), native.DefaultEntryOptions.Duration);
    }

    /// <summary>
    /// 即使早期回调未设置策略，后续回调配置策略仍必须失败。
    /// </summary>
    [Fact]
    public void LastOptionsCallbackCannotIntroduceUnsupportedProvider()
    {
        using var provider = TestCache.Create(native: builder => builder
            .WithOptions(o => o.DefaultEntryOptionsProvider = null)
            .WithOptions(o => o.DefaultEntryOptionsProvider = Mock.Of<FusionCacheEntryOptionsProvider>()));
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IHybridCache>());
        Assert.Contains("DefaultEntryOptionsProvider", error.Message);
    }
}
