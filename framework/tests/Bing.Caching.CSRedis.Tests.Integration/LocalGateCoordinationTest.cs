using Bing.Caching.CSRedis;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 本地协调门恢复测试。
/// </summary>
/// <remarks>
/// 覆盖同步等待超时、异步等待超时和取消后的协调状态恢复。
/// </remarks>
public sealed class LocalGateCoordinationTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建待测缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="LocalGateCoordinationTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供协调测试创建缓存管理器的 Redis 客户端。</param>
    public LocalGateCoordinationTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证同步相同键回源的等待超时清理。
    /// </summary>
    /// <remarks>
    /// 等待者超时后应释放本地协调状态，并允许后续读取成功。
    /// </remarks>
    [Fact]
    public async Task SyncLocalGateWait_HonorsWaitTimeoutAndCleansUp()
    {
        var configuration = CreateConfiguration("csredis-sync-gate");
        var cache = new CSRedisCacheManager(_client, configuration);
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
    /// 验证异步相同键回源的超时和取消清理。
    /// </summary>
    /// <remarks>
    /// 等待者超时或取消后应释放本地协调状态，并允许后续读取成功。
    /// </remarks>
    [Fact]
    public async Task AsyncLocalGateWait_HonorsTimeoutCancellationAndCleansUp()
    {
        var configuration = CreateConfiguration("csredis-async-gate");
        var cache = new CSRedisCacheManager(_client, configuration);
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
    /// 创建本地协调门测试配置。
    /// </summary>
    /// <remarks>
    /// 配置使用短等待时限和较长回源时限，以便覆盖等待超时路径。
    /// </remarks>
    /// <param name="name">配置命名空间使用的名称前缀。</param>
    /// <returns>包含随机命名空间和协调时限的缓存配置。</returns>
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
