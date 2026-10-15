using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Loader;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证活动插件调用未结束时不会提前释放旧代。
/// </summary>
[Collection("Bing启动期插件")]
public class HotPluginPendingCleanupTest
{
    /// <summary>
    /// 验证取消等待不会将仍在执行的旧代标记为已清理。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_NonCooperativeCall_ShouldKeepRetiringGenerationUntilCallExits()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Pending", "1.0.0",
            new[] { "Bing.Core.PluginFixtures.FixtureTopModule" });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var oldId = host.Status.Current.Id;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        WeakReference retiredContext = null;
        using var cancellation = new CancellationTokenSource();
        var active = host.RunAsync(async (provider, _) =>
        {
            retiredContext = new WeakReference(AssemblyLoadContext.GetLoadContext(provider
                .GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly));
            entered.TrySetResult(true);
            await release.Task;
            return true;
        }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reload = host.ReloadAsync();
        try
        {
            await WaitForRetirementAsync(host);
            cancellation.Cancel();
            var timedOut = await Task.WhenAny(reload, Task.Delay(TimeSpan.FromMilliseconds(100)));
            Assert.NotSame(reload, timedOut);
            Assert.Equal(oldId, host.Status.Retiring.Single().Id);
            Assert.Equal(1, host.Status.Retiring.Single().ActiveCalls);
            Assert.False(reload.IsCompleted);
        }
        finally { release.TrySetResult(true); }

        Assert.True(await active);
        await reload.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(host.Status.Retiring);
        active = null;
        await AssertCollectedAsync(retiredContext);
    }

    /// <summary>
    /// 等待旧插件代进入退役状态。
    /// </summary>
    private static async Task WaitForRetirementAsync(BingHotPluginHost<PluginHostModule> host)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (host.Status.Retiring.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.Single(host.Status.Retiring);
    }

    /// <summary>
    /// 在有界重试中确认旧代加载上下文已经回收。
    /// </summary>
    private static async Task AssertCollectedAsync(WeakReference reference)
    {
        for (var attempt = 0; attempt < 20 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (reference.IsAlive)
                await Task.Delay(25);
        }

        Assert.False(reference.IsAlive);
    }
}
