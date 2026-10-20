using System.Net;
using Bing.Caching;
using EasyCaching.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片扫描在取消后不会追加无意义的身份探测。
/// </summary>
public sealed class ShardedScanCancellationUnitTest
{
    /// <summary>
    /// 取消前缀流后跳过结尾的分片身份探测。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CancellationSkipsTrailingIdentityProbe()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = fixture.Cache
            .GetByPrefixStreamAsync<int>("item:", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        var probesAfterFirstEntry = fixture.AsyncInfoCalls;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());

        Assert.Equal(probesAfterFirstEntry, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 完整消费前缀流后执行结尾的分片身份探测。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_CompleteConsumptionKeepsTrailingIdentityProbe()
    {
        var fixture = CreateFixture();

        var entries = new List<CacheEntry<int>>();
        await foreach (var entry in fixture.Cache.GetByPrefixStreamAsync<int>("item:"))
            entries.Add(entry);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.Hit));
        Assert.All(entries, entry => Assert.Equal(1, entry.Value));
        Assert.Equal(4, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 按输入顺序返回来自各分片的批量读取结果。
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsValuesFromAllShardsInInputOrder()
    {
        var fixture = CreateFixture();

        var values = await fixture.Cache.GetAllAsync<int>(new[] { "item:0", "item:1" });

        Assert.Equal(new[] { 1, 1 }, values);
        Assert.Equal(8, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 跨分片前缀读取共用扫描资源预算。
    /// </summary>
    [Fact]
    public async Task GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget()
    {
        var fixture = CreateFixture(maxScanKeys: 2);

        var values = await fixture.Cache.GetByPrefixAsync<int>("item:");

        Assert.Equal(new[] { 1, 1 }, values);
        Assert.Equal(4, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 同步前缀读取复用异步分片读取路径。
    /// </summary>
    [Fact]
    public void GetByPrefix_SharesAsyncShardReadPath()
    {
        var fixture = CreateFixture();

        var values = fixture.Cache.GetByPrefix<int>("item:");

        Assert.Equal(new[] { 1, 1 }, values);
        Assert.Equal(4, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 前缀流在所有分片间共用扫描资源预算。
    /// </summary>
    [Fact]
    public async Task GetByPrefixStream_UsesOneScanBudgetAcrossShards()
    {
        var fixture = CreateFixture(maxScanKeys: 1);
        await using var enumerator = fixture.Cache.GetByPrefixStreamAsync<int>("item:")
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        var error = await Assert.ThrowsAsync<CacheScanLimitExceededException>(
            () => enumerator.MoveNextAsync().AsTask());

        Assert.Contains("MaxScanKeys", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, error.Limit);
        Assert.Equal(1, error.Consumed);
        Assert.Empty(error.CompletedShards);
        Assert.Contains(error.FailedShard, new int?[] { 0, 1 });
    }

    /// <summary>
    /// 跨分片前缀删除共用扫描资源预算。
    /// </summary>
    [Fact]
    public void RemoveByPrefix_UsesOneScanBudgetAcrossShards()
    {
        var fixture = CreateFixture(maxScanKeys: 1);

        var error = Assert.Throws<CacheScanLimitExceededException>(() => fixture.Cache.RemoveByPrefix("item:"));

        Assert.Contains("MaxScanKeys", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, error.Limit);
        Assert.Equal(1, error.Consumed);
        Assert.Equal(new[] { 0 }, error.CompletedShards);
        Assert.Equal(1, error.FailedShard);
    }

    /// <summary>
    /// 取消异步前缀删除后跳过结尾的分片身份探测。
    /// </summary>
    [Fact]
    public async Task RemoveByPrefixAsync_CancellationSkipsTrailingIdentityProbe()
    {
        var fixture = CreateFixture(cancelOnRemove: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Cache.RemoveByPrefixAsync("item:", fixture.Cancellation.Token));

        Assert.Equal(2, fixture.AsyncInfoCalls);
    }

    /// <summary>
    /// 同步前缀删除失败时保留原异常并跳过结尾身份探测。
    /// </summary>
    [Fact]
    public void RemoveByPrefix_SyncFailurePreservesPrimaryExceptionAndSkipsTrailingIdentityProbe()
    {
        var fixture = CreateFixture(failOnSyncRemove: true);
        var probesBeforeAction = fixture.SyncInfoCalls;

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Cache.RemoveByPrefix("item:"));

        Assert.Equal("sync delete failed", error.Message);
        Assert.Equal(probesBeforeAction + 2, fixture.SyncInfoCalls);
    }

    /// <summary>
    /// 创建可配置取消和同步删除故障的分片扫描测试夹具。
    /// </summary>
    /// <param name="cancelOnRemove">异步删除时是否取消夹具令牌。</param>
    /// <param name="failOnSyncRemove">同步删除时是否抛出异常。</param>
    /// <param name="maxScanKeys">扫描允许接受的最大逻辑键数。</param>
    /// <returns>包含分片缓存、取消令牌和调用计数器的夹具。</returns>
    private static Fixture CreateFixture(bool cancelOnRemove = false, bool failOnSyncRemove = false,
        int maxScanKeys = 0)
    {
        var configuration = new CacheConfiguration("shard-scan-cancel", "redis", batchSize: 1,
            topology: CacheTopology.ClientSharded, expectedShardCount: 2,
            resources: new CacheResourceOptions(maxScanKeys: maxScanKeys));
        var innerConfiguration = new CacheConfiguration(configuration.Namespace, configuration.ProviderName,
            configuration.DefaultExpiration, configuration.BatchSize, configuration.MaxConcurrency,
            configuration.Coordination, CacheTopology.SingleNode, resources: configuration.Resources)
            .WithInputLimits(configuration.InputLimits);
        var cancellation = new CancellationTokenSource();
        var counter = new Counter();
        var managers = new List<CacheManager>();
        var databases = new List<IDatabase>();
        for (var index = 0; index < 2; index++)
        {
            var physicalKey = CacheKeyFormat.GetPhysicalKey(innerConfiguration, "item:" + index);
            var provider = new Mock<IEasyCachingProvider>();
            provider.SetupGet(value => value.Database).Returns((object)null);
            provider.Setup(value => value.GetAllKeysByPrefixAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { physicalKey });
            provider.Setup(value => value.GetAllKeysByPrefix(It.IsAny<string>()))
                .Returns(new[] { physicalKey });
            provider.Setup(value => value.GetAllAsync<int>(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<string> keys, CancellationToken _) =>
                    Task.FromResult<IDictionary<string, CacheValue<int>>>(keys.ToDictionary(
                        key => key, _ => new CacheValue<int>(1, true))));
            provider.Setup(value => value.RemoveAllAsync(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<string> _, CancellationToken _) =>
                {
                    if (cancelOnRemove)
                        cancellation.Cancel();
                    return Task.CompletedTask;
                });
            provider.Setup(value => value.RemoveAll(It.IsAny<IEnumerable<string>>()))
                .Callback(() =>
                {
                    if (failOnSyncRemove)
                        throw new InvalidOperationException("sync delete failed");
                });
            managers.Add(new CacheManager(provider.Object, innerConfiguration));
            databases.Add(CreateDatabase(index,
                () => Interlocked.Increment(ref counter.SyncInfoCalls),
                () => Interlocked.Increment(ref counter.AsyncInfoCalls)));
        }

        return new Fixture(new ShardedRedisCacheManager(managers, databases, configuration),
            cancellation, counter);
    }

    /// <summary>
    /// 创建记录 INFO 调用次数的 Redis 数据库替身。
    /// </summary>
    /// <param name="index">用于生成端口和运行标识的分片索引。</param>
    /// <param name="onSyncInfo">同步 INFO 查询时执行的计数回调。</param>
    /// <param name="onAsyncInfo">异步 INFO 查询时执行的计数回调。</param>
    /// <returns>返回配置了同步与异步 INFO 响应的数据库替身。</returns>
    private static IDatabase CreateDatabase(int index, Action onSyncInfo, Action onAsyncInfo)
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
            .Callback(onSyncInfo)
            .Returns(RedisResult.Create((RedisValue)("# Server\r\nrun_id:scan-" + index + "\r\n")));
        database.Setup(value => value.ExecuteAsync(
                "INFO",
                It.Is<object[]>(arguments => arguments.Length == 1 &&
                    string.Equals(arguments[0] == null ? null : arguments[0].ToString(),
                        "SERVER", StringComparison.Ordinal))))
            .Callback(onAsyncInfo)
            .Returns(() => Task.FromResult(RedisResult.Create(
                (RedisValue)("# Server\r\nrun_id:scan-" + index + "\r\n"))));
        return database.Object;
    }

    /// <summary>
    /// 保存分片扫描测试所需的缓存、取消令牌和调用计数器。
    /// </summary>
    private sealed class Fixture
    {
        /// <summary>
        /// 初始化一个 <see cref="Fixture"/> 类型的实例。
        /// </summary>
        /// <param name="cache">待测试的缓存实例。</param>
        /// <param name="cancellation">用于控制异步扫描的取消令牌源。</param>
        /// <param name="counter">记录服务器信息查询次数的计数器。</param>
        internal Fixture(ShardedRedisCacheManager cache, CancellationTokenSource cancellation,
            Counter counter)
        {
            Cache = cache;
            Cancellation = cancellation;
            Counter = counter;
        }

        /// <summary>
        /// 获取待测的客户端分片缓存。
        /// </summary>
        internal ShardedRedisCacheManager Cache { get; }
        /// <summary>
        /// 获取控制异步操作的取消令牌源。
        /// </summary>
        internal CancellationTokenSource Cancellation { get; }
        /// <summary>
        /// 获取服务器信息查询计数器。
        /// </summary>
        internal Counter Counter { get; }
        /// <summary>
        /// 获取同步服务器信息调用次数。
        /// </summary>
        internal int SyncInfoCalls => Volatile.Read(ref Counter.SyncInfoCalls);
        /// <summary>
        /// 获取异步服务器信息调用次数。
        /// </summary>
        internal int AsyncInfoCalls => Volatile.Read(ref Counter.AsyncInfoCalls);
    }

    /// <summary>
    /// 记录各分片同步和异步 INFO 查询次数。
    /// </summary>
    private sealed class Counter
    {
        /// <summary>
        /// 记录同步 INFO 查询次数。
        /// </summary>
        internal int SyncInfoCalls;
        /// <summary>
        /// 记录异步 INFO 查询次数。
        /// </summary>
        internal int AsyncInfoCalls;
    }
}
