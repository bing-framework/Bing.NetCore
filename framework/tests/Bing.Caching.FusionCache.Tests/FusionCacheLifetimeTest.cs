using Bing.EasyCaching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存与依赖注入容器的资源生命周期边界。
/// </summary>
public class FusionCacheLifetimeTest
{
    /// <summary>
    /// 验证外部内存缓存只由依赖注入所有者释放一次，门面本身不持有释放职责。
    /// </summary>
    [Fact]
    public async Task ExternalMemoryCache_IsDisposedOnceByItsDiOwner()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TrackedMemoryCache>();
        services.AddBingFusionCache("one", o => { o.Namespace = "life"; o.UseAsDefault = true; },
            b => b.WithMemoryCache(sp => sp.GetRequiredService<TrackedMemoryCache>()));
        var provider = services.BuildServiceProvider();
        var memory = provider.GetRequiredService<TrackedMemoryCache>();
        var facade = provider.GetRequiredService<IHybridCache>();
        await facade.SetAsync("key", 1);
        var native = provider.GetRequiredService<IFusionCacheProvider>().GetCache("one");
        provider.Dispose();
        Assert.Equal(1, memory.DisposeCount);
        Assert.False(facade is IDisposable);
        Assert.Throws<ObjectDisposedException>(() => native.GetOrDefault<int>("key"));
    }

    /// <summary>
    /// 验证旧 EasyCaching 与混合缓存可按任意注册顺序共存且互不影响。
    /// </summary>
    [Fact]
    public async Task LegacyEasyCachingAndFusionResolveTogether_InEitherRegistrationOrder()
    {
        foreach (var fusionFirst in new[] { true, false })
        {
            var services = new ServiceCollection();
            services.AddLogging();
            void AddFusion() => services.AddBingFusionCache("catalog", o => { o.Namespace = "coexist"; o.UseAsDefault = true; });
            void AddLegacy() => services.AddMemoryCaching(new CacheConfiguration("coexist", "legacy"));
            if (fusionFirst) { AddFusion(); AddLegacy(); }
            else { AddLegacy(); AddFusion(); }
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var legacy = scope.ServiceProvider.GetRequiredService<ICache>();
            var local = scope.ServiceProvider.GetRequiredService<ILocalCache>();
            var fusion = scope.ServiceProvider.GetRequiredService<IHybridCache>();
            legacy.Set("key", 1);
            await fusion.SetAsync("key", 2);
            Assert.Equal(1, legacy.Get<int>("key"));
            Assert.Equal(1, local.Get<int>("key"));
            Assert.Equal(2, (await fusion.TryGetAsync<int>("key")).Value);
            await fusion.ClearAsync();
            Assert.Equal(1, legacy.Get<int>("key"));
        }
    }

    /// <summary>
    /// 带释放计数的内存缓存，用于验证依赖注入所有者只释放一次。
    /// </summary>
    private sealed class TrackedMemoryCache : IMemoryCache
    {
        /// <summary>
        /// 实际承载缓存条目的内部内存缓存。
        /// </summary>
        private readonly MemoryCache _inner = new(new MemoryCacheOptions { SizeLimit = 10 });

        /// <summary>
        /// 获取当前实例被释放的次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// <para>创建带单位大小的缓存条目。</para>
        /// </remarks>
        public ICacheEntry CreateEntry(object key)
        {
            var entry = _inner.CreateEntry(key);
            entry.Size = 1;
            return entry;
        }

        /// <inheritdoc />
        public void Remove(object key) => _inner.Remove(key);

        /// <inheritdoc />
        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

        /// <inheritdoc />
        /// <remarks>
        /// <para>记录并执行内部缓存释放。</para>
        /// </remarks>
        public void Dispose() { DisposeCount++; _inner.Dispose(); }
    }
}
