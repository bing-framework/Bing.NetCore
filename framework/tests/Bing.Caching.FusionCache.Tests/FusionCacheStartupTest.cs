using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存启动阶段对 Backplane 首次订阅的等待和失败处理。
/// </summary>
public class FusionCacheStartupTest
{
    /// <summary>
    /// 验证启动会等待首次订阅完成，退订外部 Backplane 但不释放其所有权。
    /// </summary>
    [Fact]
    public async Task StartupWaitsForSubscribe_AndUnsubscribesWithoutDisposingExternalBackplane()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backplane = new Mock<IFusionCacheBackplane>();
        var disposable = backplane.As<IDisposable>();
        backplane.Setup(x => x.Subscribe(It.IsAny<BackplaneSubscriptionOptions>())).Callback(() =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        });
        var provider = TestCache.Create(native: b => b.WithBackplane(backplane.Object));
        var start = Task.Run(() => provider.GetServices<IHostedService>().ToArray());
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(start.IsCompleted);
        }
        finally { release.Set(); }
        foreach (var service in await start) await service.StartAsync(default);
        provider.Dispose();
        backplane.Verify(x => x.Unsubscribe(), Times.Once);
        disposable.Verify(x => x.Dispose(), Times.Never);
    }

    /// <summary>
    /// 验证首次订阅失败会使启动失败，且不允许关闭同步订阅等待。
    /// </summary>
    [Fact]
    public void FailedInitialSubscribeFailsStartup_AndAsyncOverrideIsRejected()
    {
        var backplane = new Mock<IFusionCacheBackplane>();
        backplane.Setup(x => x.Subscribe(It.IsAny<BackplaneSubscriptionOptions>())).Throws(new IOException("subscription failed"));
        using var provider = TestCache.Create(native: b => b.WithBackplane(backplane.Object));
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>().ToArray());
        Assert.IsType<IOException>(error.InnerException);
        using var asynchronous = TestCache.Create(native: b => b.WithOptions(o => o.WaitForInitialBackplaneSubscribe = false));
        Assert.Throws<InvalidOperationException>(() => asynchronous.GetRequiredService<IHybridCache>());
    }
}
