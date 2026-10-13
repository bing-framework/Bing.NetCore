using System.Net;
using Bing.Caching;
using EasyCaching.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片异步清理使用配置的有界并发，而不是逐分片等待。
/// </summary>
public sealed class ShardedCleanupConcurrencyUnitTest
{
    /// <summary>
    /// 异步前缀删除在配置的并发上限内派发各分片操作。
    /// </summary>
    [Fact]
    public async Task RemoveByPrefixAsync_DispatchesShardsWithinConfiguredLimit()
    {
        await AssertAllShardsUseBoundedConcurrencyAsync(
            cache => cache.RemoveByPrefixAsync("item:"));
    }

    /// <summary>
    /// 异步模式删除在配置的并发上限内派发各分片操作。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_DispatchesShardsWithinConfiguredLimit()
    {
        await AssertAllShardsUseBoundedConcurrencyAsync(
            cache => cache.RemoveByPatternAsync("item:*"));
    }

    /// <summary>
    /// 异步命名空间清理在配置的并发上限内派发各分片操作。
    /// </summary>
    [Fact]
    public async Task ClearAsync_DispatchesShardsWithinConfiguredLimit()
    {
        await AssertAllShardsUseBoundedConcurrencyAsync(cache => cache.ClearAsync());
    }

    /// <summary>
    /// 异步前缀删除取消后停止派发尾部分片。
    /// </summary>
    [Fact]
    public async Task RemoveByPrefixAsync_CancellationStopsTrailingShardDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        var tracker = new CleanupTracker(blockedCalls: 1, cancelOnCall: 1, cancellation);
        var fixture = CreateFixture(tracker, shardCount: 3);

        try
        {
            var operation = fixture.Cache.RemoveByPrefixAsync("item:", cancellation.Token);
            await tracker.WaitForCallsAsync(1);

            Assert.False(operation.IsCompleted);
            Assert.Equal(1, tracker.Calls);
            tracker.ReleaseBlockedCalls();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal(1, tracker.Calls);
        }
        finally
        {
            tracker.ReleaseBlockedCalls();
        }
    }

    /// <summary>
    /// 异步模式删除观察已发出分片后传播首个失败。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_PropagatesFailureAfterObservingIssuedShards()
    {
        var failure = new InvalidOperationException("pattern delete failed");
        var tracker = new CleanupTracker(blockedCalls: 2, failureCall: 1, failure: failure);
        var fixture = CreateFixture(tracker, shardCount: 3);

        try
        {
            var operation = fixture.Cache.RemoveByPatternAsync("item:*");
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
    /// 异步模式删除首个失败后及时取消其他已发出的分片操作。
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_FailureCancelsOtherIssuedShardPromptly()
    {
        var failure = new InvalidOperationException("pattern delete failed");
        var tracker = new CleanupTracker(blockedCalls: 2, failureCall: 1, failure: failure,
            observeCancellationOnCall: 2);
        var fixture = CreateFixture(tracker, shardCount: 3);

        try
        {
            var operation = fixture.Cache.RemoveByPatternAsync("item:*");
            await tracker.WaitForCallsAsync(2);
            await tracker.WaitForCancellationAsync();

            Assert.False(operation.IsCompleted);
            tracker.ReleaseBlockedCalls();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
            Assert.Same(failure, error);
        }
        finally
        {
            tracker.ReleaseBlockedCalls();
        }
    }

    /// <summary>
    /// 断言所有分片清理操作遵守配置的并发上限。
    /// </summary>
    /// <param name="operation">待执行操作。</param>
    private static async Task AssertAllShardsUseBoundedConcurrencyAsync(
        Func<ShardedRedisCacheManager, Task> operation)
    {
        var tracker = new CleanupTracker(blockedCalls: 2);
        var fixture = CreateFixture(tracker);

        try
        {
            var pending = operation(fixture.Cache);
            await tracker.WaitForCallsAsync(2);

            Assert.False(pending.IsCompleted);
            Assert.Equal(2, tracker.Calls);
            Assert.Equal(2, tracker.MaxActive);

            tracker.ReleaseBlockedCalls();
            await pending;

            Assert.Equal(3, tracker.Calls);
            Assert.Equal(2, tracker.MaxActive);
        }
        finally
        {
            tracker.ReleaseBlockedCalls();
        }
    }

    /// <summary>
    /// 创建绑定跟踪器和多个分片数据库的清理夹具。
    /// </summary>
    /// <param name="tracker">状态跟踪器。</param>
    /// <param name="shardCount">分片数量。</param>
    /// <returns>返回绑定跟踪器和分片数据库的清理夹具。</returns>
    private static Fixture CreateFixture(CleanupTracker tracker, int shardCount = 3)
    {
        var configuration = new CacheConfiguration("sharded-cleanup-unit", "redis", batchSize: 1,
            maxConcurrency: 2, topology: CacheTopology.ClientSharded, expectedShardCount: shardCount);
        var innerConfiguration = new CacheConfiguration(configuration.Namespace,
            configuration.ProviderName, configuration.DefaultExpiration, configuration.BatchSize,
            configuration.MaxConcurrency, configuration.Coordination, CacheTopology.SingleNode,
            resources: configuration.Resources).WithInputLimits(configuration.InputLimits);
        var managers = new CacheManager[shardCount];
        var databases = new IDatabase[shardCount];
        for (var index = 0; index < shardCount; index++)
        {
            var physicalKey = CacheKeyFormat.GetPhysicalKey(innerConfiguration, "item:" + index);
            var provider = new Mock<IEasyCachingProvider>();
            provider.SetupGet(value => value.Database).Returns((object)null);
            provider.Setup(value => value.GetAllKeysByPrefixAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { physicalKey });
            provider.Setup(value => value.RemoveAllAsync(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<string> keys, CancellationToken token) => tracker.Enqueue(keys, token));
            managers[index] = new CacheManager(provider.Object, innerConfiguration);
            databases[index] = CreateDatabase(index);
        }

        return new Fixture(new ShardedRedisCacheManager(managers, databases, configuration));
    }

    /// <summary>
    /// 创建记录清理调用的 Redis 数据库替身。
    /// </summary>
    /// <param name="index">索引。</param>
    /// <returns>返回记录清理调用的数据库替身。</returns>
    private static IDatabase CreateDatabase(int index)
    {
        var endpoint = new DnsEndPoint("localhost", 6400 + index);
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
            .Returns(RedisResult.Create((RedisValue)("# Server\r\nrun_id:cleanup-" + index + "\r\n")));
        database.Setup(value => value.ExecuteAsync(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Returns(() => Task.FromResult(RedisResult.Create(
                (RedisValue)("# Server\r\nrun_id:cleanup-" + index + "\r\n"))));
        return database.Object;
    }

    /// <summary>
    /// 保存分片清理并发测试所需的缓存。
    /// </summary>
    /// <param name="Cache">缓存实例。</param>
    private sealed record Fixture(ShardedRedisCacheManager Cache);

    /// <summary>
    /// 跟踪分片清理调用的并发、取消和故障状态。
    /// </summary>
    private sealed class CleanupTracker
    {
        /// <summary>
        /// 保护清理跟踪状态的同步门。
        /// </summary>
        private readonly object _gate = new();
        /// <summary>
        /// 触发阻塞的清理调用数量。
        /// </summary>
        private readonly int _blockedCalls;
        /// <summary>
        /// 触发取消的调用序号；零表示不触发取消。
        /// </summary>
        private readonly int _cancelOnCall;
        /// <summary>
        /// 用于触发清理操作取消的令牌源。
        /// </summary>
        private readonly CancellationTokenSource _cancellation;
        /// <summary>
        /// 触发故障的调用序号；零表示不触发故障。
        /// </summary>
        private readonly int _failureCall;
        /// <summary>
        /// 触发取消观察信号的调用序号；零表示不触发。
        /// </summary>
        private readonly int _observeCancellationOnCall;
        /// <summary>
        /// 触发故障时抛出的异常。
        /// </summary>
        private readonly Exception _failure;
        /// <summary>
        /// 在指定调用观察到取消后的完成信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _cancellationObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        /// 初始化一个 <see cref="CleanupTracker"/> 类型的实例。
        /// </summary>
        /// <param name="blockedCalls">需要阻塞的调用数量。</param>
        /// <param name="cancelOnCall">触发取消的调用序号；零表示不触发。</param>
        /// <param name="cancellation">用于触发取消的令牌源。</param>
        /// <param name="failureCall">触发故障的调用序号；零表示不触发。</param>
        /// <param name="failure">故障信息。</param>
        /// <param name="observeCancellationOnCall">触发取消观察信号的调用序号；零表示不触发。</param>
        internal CleanupTracker(int blockedCalls, int cancelOnCall = 0,
            CancellationTokenSource cancellation = null, int failureCall = 0,
            Exception failure = null, int observeCancellationOnCall = 0)
        {
            _blockedCalls = blockedCalls;
            _cancelOnCall = cancelOnCall;
            _cancellation = cancellation;
            _failureCall = failureCall;
            _failure = failure;
            _observeCancellationOnCall = observeCancellationOnCall;
        }

        /// <summary>
        /// 获取已进入跟踪器的调用次数。
        /// </summary>
        internal int Calls => Volatile.Read(ref _calls);
        /// <summary>
        /// 获取记录到的最大并发调用数。
        /// </summary>
        internal int MaxActive => Volatile.Read(ref _maxActive);

        /// <summary>
        /// 记录清理批次并返回等待其释放的完成任务。
        /// </summary>
        /// <param name="keys">逻辑缓存键集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        internal Task Enqueue(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == _observeCancellationOnCall)
                cancellationToken.Register(() => _cancellationObserved.TrySetResult(true));
            if (call == _cancelOnCall)
                _cancellation.Cancel();
            if (call == _failureCall)
                return Task.FromException(_failure);
            if (call > _blockedCalls)
                return Task.CompletedTask;

            Enter();
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _blocked.Add(completion);
            return AwaitBlockedAsync(completion.Task);
        }

        /// <summary>
        /// 等待跟踪器观察到取消。
        /// </summary>
        internal async Task WaitForCancellationAsync()
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(5));
            var completed = await Task.WhenAny(_cancellationObserved.Task, timeout)
                .ConfigureAwait(false);
            if (!ReferenceEquals(completed, _cancellationObserved.Task))
                throw new TimeoutException("等待已派发清理分片收到取消信号超时。");
            await _cancellationObserved.Task.ConfigureAwait(false);
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
                    throw new TimeoutException($"等待清理批量调用数达到 {expected} 超时，实际为 {Calls}。");
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

        /// <summary>
        /// 等待阻塞调用达到指定数量。
        /// </summary>
        /// <param name="completion">完成信号。</param>
        private async Task AwaitBlockedAsync(Task completion)
        {
            try
            {
                await completion.ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        /// <summary>
        /// 记录一次活动调用并应用取消或故障规则。
        /// </summary>
        private void Enter()
        {
            var active = Interlocked.Increment(ref _active);
            while (true)
            {
                var current = Volatile.Read(ref _maxActive);
                if (active <= current ||
                    Interlocked.CompareExchange(ref _maxActive, active, current) == current)
                    return;
            }
        }
    }
}
