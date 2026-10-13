using System.Net;
using Bing.Caching;
using EasyCaching.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片同步清理的显式有界并发契约。
/// </summary>
public sealed class ShardedSyncCleanupConcurrencyUnitTest
{
    /// <summary>
    /// 同步清理在配置的并发上限内处理全部分片。
    /// </summary>
    [Fact]
    public async Task BoundedParallel_CleanupDoesNotExceedConfiguredConcurrency()
    {
        var tracker = new SyncCleanupTracker(blockedCalls: 2);
        var fixture = CreateFixture(tracker);
        var operation = Start(() => fixture.Cache.RemoveByPrefix("item:"));

        try
        {
            await tracker.WaitForCallsAsync(2);

            Assert.False(operation.IsCompleted);
            Assert.Equal(2, tracker.Calls);
            Assert.Equal(2, tracker.MaxActive);

            tracker.ReleaseBlockedCalls();
            await operation;

            Assert.Equal(3, tracker.Calls);
            Assert.Equal(2, tracker.MaxActive);
        }
        finally
        {
            tracker.ReleaseBlockedCalls();
        }
    }

    /// <summary>
    /// 同步清理首个失败后停止尾部分片，并观察已发出的窗口。
    /// </summary>
    [Fact]
    public async Task BoundedParallel_FailureStopsTrailingShardAndObservesWindow()
    {
        var failure = new InvalidOperationException("sync shard delete failed");
        var tracker = new SyncCleanupTracker(blockedCalls: 2, failureCall: 1, failure);
        var fixture = CreateFixture(tracker);
        var operation = Start(() => fixture.Cache.RemoveByPattern("item:*"));

        try
        {
            await tracker.WaitForCallsAsync(2);

            Assert.False(operation.IsCompleted);
            Assert.Equal(2, tracker.Calls);

            tracker.ReleaseBlockedCalls();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);

            Assert.Same(failure, error);
            Assert.Equal(2, tracker.Calls);
        }
        finally
        {
            tracker.ReleaseBlockedCalls();
        }
    }

    /// <summary>
    /// 同步清理覆盖前缀、模式和命名空间清理路径。
    /// </summary>
    [Fact]
    public async Task BoundedParallel_CoversPrefixPatternAndClear()
    {
        foreach (var operationFactory in new Func<ShardedRedisCacheManager, Action>[]
        {
            cache => () => cache.RemoveByPrefix("item:"),
            cache => () => cache.RemoveByPattern("item:*"),
            cache => cache.Clear
        })
        {
            var tracker = new SyncCleanupTracker(blockedCalls: 2);
            var fixture = CreateFixture(tracker);
            var operation = Start(operationFactory(fixture.Cache));
            try
            {
                await tracker.WaitForCallsAsync(2);
                Assert.Equal(2, tracker.MaxActive);
                tracker.ReleaseBlockedCalls();
                await operation;
            }
            finally
            {
                tracker.ReleaseBlockedCalls();
            }
        }
    }

    /// <summary>
    /// 在后台启动同步测试操作。
    /// </summary>
    /// <param name="action">待执行操作。</param>
    private static Task Start(Action action) => Task.Factory.StartNew(action,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// 创建绑定跟踪器和多个分片数据库的同步清理夹具。
    /// </summary>
    /// <param name="tracker">状态跟踪器。</param>
    /// <returns>返回绑定跟踪器和分片数据库的同步清理夹具。</returns>
    private static Fixture CreateFixture(SyncCleanupTracker tracker)
    {
        const int shardCount = 3;
        var configuration = new CacheConfiguration("sharded-sync-cleanup", "redis", batchSize: 1,
            maxConcurrency: 2, topology: CacheTopology.ClientSharded, expectedShardCount: shardCount)
            .WithSyncCleanupMode(CacheSyncCleanupMode.BoundedParallel);
        var innerConfiguration = new CacheConfiguration(configuration.Namespace,
            configuration.ProviderName, configuration.DefaultExpiration, configuration.BatchSize,
            configuration.MaxConcurrency, configuration.Coordination, CacheTopology.SingleNode,
            resources: configuration.Resources)
            .WithInputLimits(configuration.InputLimits)
            .WithSyncCleanupMode(configuration.SyncCleanupMode);
        var managers = new CacheManager[shardCount];
        var databases = new IDatabase[shardCount];
        for (var index = 0; index < shardCount; index++)
        {
            var physicalKey = CacheKeyFormat.GetPhysicalKey(innerConfiguration, "item:" + index);
            var provider = new Mock<IEasyCachingProvider>();
            provider.SetupGet(value => value.Database).Returns((object)null);
            provider.Setup(value => value.GetAllKeysByPrefix(It.IsAny<string>()))
                .Returns(new[] { physicalKey });
            provider.Setup(value => value.RemoveAll(It.IsAny<IEnumerable<string>>()))
                .Callback((IEnumerable<string> keys) => tracker.Remove(keys));
            managers[index] = new CacheManager(provider.Object, innerConfiguration);
            databases[index] = CreateDatabase(index);
        }

        return new Fixture(new ShardedRedisCacheManager(managers, databases, configuration));
    }

    /// <summary>
    /// 创建记录同步清理调用的 Redis 数据库替身。
    /// </summary>
    /// <param name="index">索引。</param>
    /// <returns>返回记录同步清理调用的数据库替身。</returns>
    private static IDatabase CreateDatabase(int index)
    {
        var endpoint = new DnsEndPoint("localhost", 6500 + index);
        var server = new Mock<IServer>();
        server.SetupGet(value => value.IsConnected).Returns(true);
        server.SetupGet(value => value.IsReplica).Returns(false);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(value => value.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { endpoint });
        multiplexer.Setup(value => value.GetServer(endpoint, It.IsAny<object>()))
            .Returns(server.Object);
        var database = new Mock<IDatabase>();
        database.SetupGet(value => value.Multiplexer).Returns(multiplexer.Object);
        database.Setup(value => value.Execute(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Returns(RedisResult.Create((RedisValue)("# Server\r\nrun_id:sync-cleanup-" + index + "\r\n")));
        return database.Object;
    }

    /// <summary>
    /// 保存分片同步清理测试所需的缓存。
    /// </summary>
    /// <param name="Cache">缓存实例。</param>
    private sealed record Fixture(ShardedRedisCacheManager Cache);

    /// <summary>
    /// 跟踪分片同步清理调用的并发和故障状态。
    /// </summary>
    private sealed class SyncCleanupTracker
    {
        /// <summary>
        /// 保护同步清理跟踪状态的同步门。
        /// </summary>
        private readonly object _gate = new();
        /// <summary>
        /// 触发阻塞的清理调用数量。
        /// </summary>
        private readonly int _blockedCalls;
        /// <summary>
        /// 触发故障的调用序号；零表示不触发故障。
        /// </summary>
        private readonly int _failureCall;
        /// <summary>
        /// 触发故障时抛出的异常。
        /// </summary>
        private readonly Exception _failure;
        /// <summary>
        /// 按调用顺序保存等待释放的清理批次完成源。
        /// </summary>
        private readonly List<TaskCompletionSource<bool>> _blocked = new();
        /// <summary>
        /// 已进入跟踪器的调用次数。
        /// </summary>
        private int _calls;
        /// <summary>
        /// 当前尚未完成的活动调用数。
        /// </summary>
        private int _active;
        /// <summary>
        /// 记录到的最大活动调用数。
        /// </summary>
        private int _maxActive;

        /// <summary>
        /// 初始化一个 <see cref="SyncCleanupTracker"/> 类型的实例。
        /// </summary>
        /// <param name="blockedCalls">需要阻塞的调用数量。</param>
        /// <param name="failureCall">触发故障的调用序号；零表示不触发。</param>
        /// <param name="failure">故障信息。</param>
        internal SyncCleanupTracker(int blockedCalls, int failureCall = 0, Exception failure = null)
        {
            _blockedCalls = blockedCalls;
            _failureCall = failureCall;
            _failure = failure;
        }

        /// <summary>
        /// 获取已进入跟踪器的调用次数。
        /// </summary>
        internal int Calls => Volatile.Read(ref _calls);
        /// <summary>
        /// 获取记录到的最大并发调用数。
        /// </summary>
        internal int MaxActive => Volatile.Read(ref _maxActive);

        /// <inheritdoc />
        /// <param name="keys">逻辑缓存键集合。</param>
        internal void Remove(IEnumerable<string> keys)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == _failureCall)
                throw _failure;
            if (call > _blockedCalls)
                return;

            var active = Interlocked.Increment(ref _active);
            while (active > Volatile.Read(ref _maxActive))
            {
                var observed = Volatile.Read(ref _maxActive);
                if (active <= observed || Interlocked.CompareExchange(ref _maxActive, active, observed) == observed)
                    break;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _blocked.Add(completion);
            try
            {
                completion.Task.GetAwaiter().GetResult();
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        /// <summary>
        /// 等待已记录调用数达到预期值。
        /// </summary>
        /// <param name="expected">预期值。</param>
        internal async Task WaitForCallsAsync(int expected)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (Calls < expected)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"等待同步清理调用数达到 {expected} 超时，实际为 {Calls}。");
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 释放当前阻塞的清理批次。
        /// </summary>
        internal void ReleaseBlockedCalls()
        {
            TaskCompletionSource<bool>[] pending;
            lock (_gate)
            {
                pending = _blocked.ToArray();
                _blocked.Clear();
            }
            foreach (var completion in pending)
                completion.TrySetResult(true);
        }
    }
}
