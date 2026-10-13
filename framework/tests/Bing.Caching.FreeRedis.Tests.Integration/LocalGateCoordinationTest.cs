using Bing.Caching.FreeRedis;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 验证 FreeRedis 同键回源门控的等待期限和状态回收。
/// </summary>
public sealed class LocalGateCoordinationTest
{
    /// <summary>
    /// 用于执行本地回源协调测试的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="LocalGateCoordinationTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public LocalGateCoordinationTest(RedisClient client) => _client = client;

    /// <summary>
    /// 同步回源等待超过期限后抛出超时，并清理门控状态。
    /// </summary>
    [Fact]
    public async Task SyncLocalGateWait_HonorsWaitTimeoutAndCleansUp()
    {
        var configuration = CreateConfiguration("freeredis-sync-gate");
        var cache = new FreeRedisCacheManager(_client, configuration);
        var key = "item:" + Guid.NewGuid().ToString("N");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Task.Run(() => cache.Get(key, () =>
        {
            entered.SetResult(true);
            release.Task.GetAwaiter().GetResult();
            return 1;
        }));
        await entered.Task;
        var waiter = Task.Run(() => cache.Get(key, () => 2));

        try
        {
            var completed = await Task.WhenAny(waiter, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waiter, completed);
            var exception = await Record.ExceptionAsync(async () => await waiter);
            Assert.IsType<TimeoutException>(exception);
        }
        finally
        {
            release.TrySetResult(true);
            await holder;
            if (!waiter.IsCompleted)
                await waiter;
        }

        cache.Remove(key);
        Assert.Equal(3, cache.Get(key, () => 3));
        cache.Remove(key);
    }

    /// <summary>
    /// 异步回源等待遵守超时和取消，并在结束后清理门控状态。
    /// </summary>
    [Fact]
    public async Task AsyncLocalGateWait_HonorsTimeoutCancellationAndCleansUp()
    {
        var configuration = CreateConfiguration("freeredis-async-gate");
        var cache = new FreeRedisCacheManager(_client, configuration);
        var key = "item:" + Guid.NewGuid().ToString("N");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = cache.GetAsync<int>(key, async _ =>
        {
            entered.SetResult(true);
            await release.Task;
            return 1;
        });
        await entered.Task;
        var timeoutWaiter = cache.GetAsync<int>(key, _ => Task.FromResult(2));

        try
        {
            var completed = await Task.WhenAny(timeoutWaiter, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(timeoutWaiter, completed);
            var timeoutException = await Record.ExceptionAsync(async () => await timeoutWaiter);
            Assert.IsType<TimeoutException>(timeoutException);

            using var cancellation = new CancellationTokenSource();
            var cancellationWaiter = cache.GetAsync<int>(key, _ => Task.FromResult(3),
                cancellationToken: cancellation.Token);
            await Task.Delay(50);
            cancellation.Cancel();
            var cancellationCompleted = await Task.WhenAny(cancellationWaiter, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(cancellationWaiter, cancellationCompleted);
            var cancellationException = await Record.ExceptionAsync(async () => await cancellationWaiter);
            Assert.IsAssignableFrom<OperationCanceledException>(cancellationException);
        }
        finally
        {
            release.TrySetResult(true);
            await holder;
        }

        cache.Remove(key);
        Assert.Equal(4, await cache.GetAsync<int>(key, _ => Task.FromResult(4)));
        cache.Remove(key);
    }

    /// <summary>
    /// 创建带有短租约和短等待期限的协调测试配置。
    /// </summary>
    /// <param name="name">名称。</param>
    /// <returns>具有有限租约、续租、等待和回源期限的缓存配置。</returns>
    private static CacheConfiguration CreateConfiguration(string name)
    {
        return new CacheConfiguration(name + "-" + Guid.NewGuid().ToString("N"), "redis",
            coordination: new CacheCoordinationOptions(
                leaseDuration: TimeSpan.FromSeconds(2),
                renewalInterval: TimeSpan.FromMilliseconds(100),
                waitTimeout: TimeSpan.FromMilliseconds(100),
                retrievalTimeout: TimeSpan.FromSeconds(5)));
    }
}
