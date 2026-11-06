using Bing.Samples.FusionCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证宿主关闭时后台回源作用域先于容器释放完成排空。
/// </summary>
public class FusionCacheShutdownTest
{
    /// <summary>
    /// 验证宿主停止会等待后台工厂释放独立作用域，再释放容器资源。
    /// </summary>
    [Fact]
    public async Task HostShutdown_DrainsBackgroundFactoryBeforeScopeAndContainerDisposal()
    {
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddScoped<TrackedScope>();
            services.AddBingFusionCache("shutdown", o => { o.Namespace = "test"; o.UseAsDefault = true; },
                b => b.WithDefaultEntryOptions(o =>
                {
                    o.IsFailSafeEnabled = true;
                    o.FactorySoftTimeout = TimeSpan.FromMilliseconds(20);
                    o.AllowTimedOutFactoryBackgroundCompletion = true;
                }));
            services.AddSingleton<SampleFactoryLifetime>();
            services.AddHostedService(sp => sp.GetRequiredService<SampleFactoryLifetime>());
        }).Build();
        await host.StartAsync();
        var cache = host.Services.GetRequiredService<IHybridCache>();
        var lifetime = host.Services.GetRequiredService<SampleFactoryLifetime>();
        var entered = TestCache.Signal<TrackedScope>();
        var release = TestCache.Signal<bool>();
        await cache.SetAsync("key", 1);
        await cache.ExpireAsync("key");
        Assert.Equal(1, await cache.GetOrCreateAsync("key", token => lifetime.RunAsync(async ct =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            var tracked = scope.ServiceProvider.GetRequiredService<TrackedScope>();
            entered.SetResult(tracked);
            await release.Task.WaitAsync(ct);
            Assert.False(tracked.Disposed);
            return 2;
        }, token)));
        var resource = await entered.Task;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stop = host.StopAsync(timeout.Token);
        Assert.False(stop.IsCompleted);
        Assert.False(resource.Disposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifetime.RunAsync(_ => Task.FromResult(3), default));
        release.SetResult(true);
        await stop;
        Assert.True(resource.Disposed);
    }

    /// <summary>
    /// 记录自身释放状态的测试作用域资源。
    /// </summary>
    public sealed class TrackedScope : IDisposable
    {
        /// <summary>
        /// 获取当前实例是否已释放。
        /// </summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// <para>标记当前实例已释放。</para>
        /// </remarks>
        public void Dispose() => Disposed = true;
    }
}
