using System.Net;
using Bing.Caching;
using EasyCaching.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片批量写入和删除的并发、取消及失败传播语义。
/// </summary>
public sealed class ShardedBatchConcurrencyUnitTest
{
    /// <summary>
    /// 批量写入限制在途批次数量，并等待全部分片完成。
    /// </summary>
    [Fact]
    public async Task SetAllAsync_LimitsInFlightBatchesAndCompletesAllShards()
    {
        var tracker = new BatchTracker(blockedCalls: 2);
        var fixture = CreateFixture(tracker);
        var items = CreateItems(8);

        var operation = fixture.Cache.SetAllAsync(items);
        await tracker.WaitForCallsAsync(2);

        Assert.Equal(2, tracker.Calls);
        Assert.Equal(2, tracker.MaxActive);
        tracker.ReleaseBlockedCalls();

        await operation;

        Assert.Equal(items.Count, tracker.Calls);
        Assert.Equal(2, tracker.MaxActive);
        Assert.All(tracker.BatchSizes, size => Assert.Equal(1, size));
    }

    /// <summary>
    /// 批量删除限制在途批次数量，并等待全部分片完成。
    /// </summary>
    [Fact]
    public async Task RemoveAllAsync_LimitsInFlightBatchesAndCompletesAllShards()
    {
        var tracker = new BatchTracker(blockedCalls: 2);
        var fixture = CreateFixture(tracker);
        var keys = CreateItems(8).Keys.ToArray();

        var operation = fixture.Cache.RemoveAllAsync(keys);
        await tracker.WaitForCallsAsync(2);

        Assert.Equal(2, tracker.Calls);
        Assert.Equal(2, tracker.MaxActive);
        tracker.ReleaseBlockedCalls();

        await operation;

        Assert.Equal(keys.Length, tracker.Calls);
        Assert.Equal(2, tracker.MaxActive);
        Assert.All(tracker.BatchSizes, size => Assert.Equal(1, size));
    }

    /// <summary>
    /// 批量写入取消后停止派发，并等待已发出的批次完成。
    /// </summary>
    [Fact]
    public async Task SetAllAsync_CancellationStopsDispatchAndWaitsForIssuedBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var tracker = new BatchTracker(blockedCalls: 1, cancelOnCall: 1, cancellation);
        var fixture = CreateFixture(tracker);
        var operation = fixture.Cache.SetAllAsync(CreateItems(8),
            cancellationToken: cancellation.Token);

        await tracker.WaitForCallsAsync(1);

        Assert.False(operation.IsCompleted);
        Assert.Equal(1, tracker.Calls);
        tracker.ReleaseBlockedCalls();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(1, tracker.Calls);
    }

    /// <summary>
    /// 批量删除取消后停止派发，并等待已发出的批次完成。
    /// </summary>
    [Fact]
    public async Task RemoveAllAsync_CancellationStopsDispatchAndWaitsForIssuedBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var tracker = new BatchTracker(blockedCalls: 1, cancelOnCall: 1, cancellation);
        var fixture = CreateFixture(tracker);
        var operation = fixture.Cache.RemoveAllAsync(CreateItems(8).Keys,
            cancellation.Token);

        await tracker.WaitForCallsAsync(1);

        Assert.False(operation.IsCompleted);
        Assert.Equal(1, tracker.Calls);
        tracker.ReleaseBlockedCalls();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(1, tracker.Calls);
    }

    /// <summary>
    /// 批量写入观察已发出批次后传播首个失败。
    /// </summary>
    [Fact]
    public async Task SetAllAsync_PropagatesFirstFailureAfterObservingIssuedBatch()
    {
        var failure = new InvalidOperationException("set batch failed");
        var tracker = new BatchTracker(blockedCalls: 2, failureCall: 1, failure: failure);
        var fixture = CreateFixture(tracker);
        var operation = fixture.Cache.SetAllAsync(CreateItems(8));

        await tracker.WaitForCallsAsync(2);

        Assert.False(operation.IsCompleted);
        Assert.Equal(2, tracker.Calls);
        tracker.ReleaseBlockedCalls();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Same(failure, error);
        Assert.Equal(2, tracker.Calls);
    }

    /// <summary>
    /// 批量删除观察已发出批次后传播首个失败。
    /// </summary>
    [Fact]
    public async Task RemoveAllAsync_PropagatesFirstFailureAfterObservingIssuedBatch()
    {
        var failure = new InvalidOperationException("remove batch failed");
        var tracker = new BatchTracker(blockedCalls: 2, failureCall: 1, failure: failure);
        var fixture = CreateFixture(tracker);
        var operation = fixture.Cache.RemoveAllAsync(CreateItems(8).Keys);

        await tracker.WaitForCallsAsync(2);

        Assert.False(operation.IsCompleted);
        Assert.Equal(2, tracker.Calls);
        tracker.ReleaseBlockedCalls();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Same(failure, error);
        Assert.Equal(2, tracker.Calls);
    }

    /// <summary>
    /// 创建绑定跟踪器和多个分片数据库的缓存夹具。
    /// </summary>
    /// <param name="tracker">状态跟踪器。</param>
    /// <returns>返回绑定跟踪器和分片数据库的缓存夹具。</returns>
    private static Fixture CreateFixture(BatchTracker tracker)
    {
        var configuration = new CacheConfiguration("sharded-batch-unit", "redis", batchSize: 1,
            maxConcurrency: 2, topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var innerConfiguration = new CacheConfiguration(configuration.Namespace,
            configuration.ProviderName, configuration.DefaultExpiration, configuration.BatchSize,
            configuration.MaxConcurrency, configuration.Coordination, CacheTopology.SingleNode,
            resources: configuration.Resources).WithInputLimits(configuration.InputLimits);
        var managers = new CacheManager[2];
        var databases = new IDatabase[2];
        for (var index = 0; index < managers.Length; index++)
        {
            var provider = new Mock<IEasyCachingProvider>();
            var database = CreateDatabase(index);
            provider.SetupGet(value => value.Database).Returns(database.Object);
            provider.Setup(value => value.SetAllAsync(
                    It.IsAny<IDictionary<string, int>>(), It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns((IDictionary<string, int> values, TimeSpan _, CancellationToken _) =>
                    tracker.Enqueue(values.Count));
            provider.Setup(value => value.RemoveAllAsync(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<string> keys, CancellationToken _) =>
                    tracker.Enqueue(keys.Count()));
            managers[index] = new CacheManager(provider.Object, innerConfiguration);
            databases[index] = database.Object;
        }

        return new Fixture(new ShardedRedisCacheManager(managers, databases, configuration));
    }

    /// <summary>
    /// 创建记录异步批量调用的 Redis 数据库替身。
    /// </summary>
    /// <param name="index">索引。</param>
    /// <returns>返回记录批量调用的数据库模拟对象。</returns>
    private static Mock<IDatabase> CreateDatabase(int index)
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
            .Returns(RedisResult.Create((RedisValue)("# Server\r\nrun_id:batch-" + index + "\r\n")));
        database.Setup(value => value.ExecuteAsync(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Returns(() => Task.FromResult(RedisResult.Create(
                (RedisValue)("# Server\r\nrun_id:batch-" + index + "\r\n"))));
        return database;
    }

    /// <summary>
    /// 创建指定数量的带序整数缓存项。
    /// </summary>
    /// <param name="count">数量。</param>
    /// <returns>返回指定数量的带序整数项。</returns>
    private static Dictionary<string, int> CreateItems(int count)
    {
        var items = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
            items.Add("item:" + index, index + 1);
        return items;
    }

    /// <summary>
    /// 保存分片批量并发测试所需的缓存。
    /// </summary>
    /// <param name="Cache">缓存实例。</param>
    private sealed record Fixture(ShardedRedisCacheManager Cache);

    /// <summary>
    /// 跟踪分片批量调用的并发、取消和故障状态。
    /// </summary>
    private sealed class BatchTracker
    {
        /// <summary>
        /// 保护批量跟踪状态的同步门。
        /// </summary>
        private readonly object _gate = new();
        /// <summary>
        /// 触发阻塞的批量调用数量。
        /// </summary>
        private readonly int _blockedCalls;
        /// <summary>
        /// 触发取消的调用序号；零表示不触发取消。
        /// </summary>
        private readonly int _cancelOnCall;
        /// <summary>
        /// 用于触发批量操作取消的令牌源。
        /// </summary>
        private readonly CancellationTokenSource _cancellation;
        /// <summary>
        /// 触发故障的调用序号；零表示不触发故障。
        /// </summary>
        private readonly int _failureCall;
        /// <summary>
        /// 触发故障时抛出的异常。
        /// </summary>
        private readonly Exception _failure;
        /// <summary>
        /// 按调用顺序保存等待释放的批次完成源。
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
        /// 初始化一个 <see cref="BatchTracker"/> 类型的实例。
        /// </summary>
        /// <param name="blockedCalls">需要阻塞的调用数量。</param>
        /// <param name="cancelOnCall">触发取消的调用序号；零表示不触发。</param>
        /// <param name="cancellation">用于触发取消的令牌源。</param>
        /// <param name="failureCall">触发故障的调用序号；零表示不触发。</param>
        /// <param name="failure">故障信息。</param>
        internal BatchTracker(int blockedCalls, int cancelOnCall = 0,
            CancellationTokenSource cancellation = null, int failureCall = 0,
            Exception failure = null)
        {
            _blockedCalls = blockedCalls;
            _cancelOnCall = cancelOnCall;
            _cancellation = cancellation;
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
        /// <summary>
        /// 获取按调用顺序记录的批次大小。
        /// </summary>
        internal IReadOnlyList<int> BatchSizes
        {
            get
            {
                lock (_gate)
                    return _batchSizes.ToArray();
            }
        }

        /// <summary>
        /// 按调用顺序记录每个批次的大小。
        /// </summary>
        private readonly List<int> _batchSizes = new();

        /// <summary>
        /// 记录批次并返回等待其释放的完成任务。
        /// </summary>
        /// <param name="batchSize">批大小。</param>
        internal Task Enqueue(int batchSize)
        {
            var call = Interlocked.Increment(ref _calls);
            lock (_gate)
                _batchSizes.Add(batchSize);
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
        /// 等待已记录调用数达到预期值。
        /// </summary>
        /// <param name="expected">预期值。</param>
        internal async Task WaitForCallsAsync(int expected)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (Calls < expected)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"等待批量调用数达到 {expected} 超时，实际为 {Calls}。");
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// 释放当前阻塞的批次调用。
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
