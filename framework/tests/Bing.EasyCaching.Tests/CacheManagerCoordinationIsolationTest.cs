using Bing.Caching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证不同缓存管理器实例之间协调状态的隔离。
/// </summary>
public sealed class CacheManagerCoordinationIsolationTest
{
    /// <summary>
    /// 不同 Provider 的相同键不会共享并发门控。
    /// </summary>
    [Fact]
    public async Task DifferentProvidersDoNotShareSameKeyGate()
    {
        var configuration = new CacheConfiguration(
            "coordination-isolation-" + Guid.NewGuid().ToString("N"), "memory");
        using var leftServices = new ServiceCollection()
            .AddLogging()
            .AddMemoryCaching(configuration)
            .BuildServiceProvider();
        using var rightServices = new ServiceCollection()
            .AddLogging()
            .AddMemoryCaching(configuration)
            .BuildServiceProvider();
        var left = (MemoryCacheManager)leftServices.GetRequiredService<ILocalCache>();
        var right = (MemoryCacheManager)rightServices.GetRequiredService<ILocalCache>();

        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leftTask = left.GetAsync("same-key", async _ =>
        {
            entered.SetResult(true);
            await release.Task;
            return 1;
        });

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var rightTask = right.GetAsync("same-key", _ => Task.FromResult(2));
        var completed = await Task.WhenAny(rightTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(rightTask, completed);
        Assert.Equal(2, await rightTask);

        release.SetResult(true);
        Assert.Equal(1, await leftTask);
    }

    /// <summary>
    /// 不同 Provider 的相同键不会共享递归状态。
    /// </summary>
    [Fact]
    public async Task DifferentProvidersDoNotShareRecursionState()
    {
        var configuration = new CacheConfiguration(
            "recursion-isolation-" + Guid.NewGuid().ToString("N"), "memory");
        using var leftServices = new ServiceCollection()
            .AddLogging()
            .AddMemoryCaching(configuration)
            .BuildServiceProvider();
        using var rightServices = new ServiceCollection()
            .AddLogging()
            .AddMemoryCaching(configuration)
            .BuildServiceProvider();
        var left = (MemoryCacheManager)leftServices.GetRequiredService<ILocalCache>();
        var right = (MemoryCacheManager)rightServices.GetRequiredService<ILocalCache>();

        var result = await left.GetAsync("same-key", async _ =>
            1 + await right.GetAsync("same-key", __ => Task.FromResult(2)));

        Assert.Equal(3, result);
    }
}
