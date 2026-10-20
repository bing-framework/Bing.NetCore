using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Serialization;
using Moq;
using StackExchange.Redis;
using System.Text;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 Redis 租约协调器的批量写入、删除和取消语义。
/// </summary>
public class RedisLeaseCoordinatorBatchTest
{
    /// <summary>
    /// 批量写入使用一条 Lua 命令和一次批量失效通知。
    /// </summary>
    [Fact]
    public void WriteBatch_UsesOneLuaCommandAndOneBatchInvalidation()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        RedisKey[] observedKeys = null;
        RedisValue[] observedValues = null;
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, keys, values, _) =>
            {
                observedKeys = keys;
                observedValues = values;
            })
            .Returns(RedisResult.Create((RedisValue)"2"));
        var invalidations = new List<string[]>();
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("batch", "redis"),
            invalidateBatch: keys => invalidations.Add(keys.ToArray()));

        coordinator.WriteBatch(new[]
        {
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2)
        }, TimeSpan.FromMilliseconds(500));

        database.Verify(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
        Assert.Equal(new[] { "one", "two", "one:lease", "two:lease" },
            observedKeys.Select(value => value.ToString()));
        Assert.Equal(4, observedValues.Length);
        Assert.Equal((RedisValue)"500", observedValues[1]);
        Assert.Equal((RedisValue)"500", observedValues[3]);
        Assert.Equal(new[] { "one", "two" }, invalidations.Single());
    }

    /// <summary>
    /// Cluster 批量写入按槽位分组键。
    /// </summary>
    [Fact]
    public void ClusterWriteBatch_GroupsKeysBySlot()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        database.Setup(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                RedisResult.Create((RedisValue)(keys.Length / 2).ToString()));
        var configuration = new CacheConfiguration("cluster-batch", "redis",
            topology: CacheTopology.RedisCluster);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = Enumerable.Range(2, 100)
            .Select(index => CacheKeyFormat.GetPhysicalKey(configuration, "key-" + index))
            .First(key => CacheClusterTopology.GetHashSlot(key) != CacheClusterTopology.GetHashSlot(first));
        var invalidations = new List<string[]>();
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration,
            invalidateBatch: keys => invalidations.Add(keys.ToArray()));

        coordinator.WriteBatch(new[]
        {
            new KeyValuePair<string, int>(first, 1),
            new KeyValuePair<string, int>(second, 2)
        }, TimeSpan.FromSeconds(1));

        database.Verify(value => value.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Exactly(2));
        Assert.Equal(2, invalidations.Count);
        Assert.All(invalidations, value => Assert.Single(value));
    }

    /// <summary>
    /// 批量删除使用一次删除命令和一次批量失效通知。
    /// </summary>
    [Fact]
    public void RemoveBatch_UsesOneDeleteAndOneBatchInvalidation()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        RedisKey[] observedKeys = null;
        database.Setup(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Callback<RedisKey[], CommandFlags>((keys, _) => observedKeys = keys)
            .Returns(4L);
        var invalidations = new List<string[]>();
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("batch-delete", "redis"),
            invalidateBatch: keys => invalidations.Add(keys.ToArray()));

        coordinator.RemoveBatch(new List<string> { "one", "two" });

        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
        Assert.Equal(new[] { "one", "two", "one:lease", "two:lease" },
            observedKeys.Select(value => value.ToString()));
        Assert.Equal(new[] { "one", "two" }, invalidations.Single());
    }

    /// <summary>
    /// 批量删除遵守 Redis 命令字节预算。
    /// </summary>
    [Fact]
    public void RemoveBatch_RespectsCommandByteBudget()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        database.Setup(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns(2L);
        var resources = new CacheResourceOptions(
            maxBatchCommandBytes: PairBytes("one"));
        var configuration = new CacheConfiguration("budget-delete", "redis", batchSize: 10,
            resources: resources);
        var invalidations = new List<string[]>();
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration,
            invalidateBatch: keys => invalidations.Add(keys.ToArray()));

        coordinator.RemoveBatch(new List<string> { "one", "two" });

        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Exactly(2));
        Assert.Equal(2, invalidations.Count);
        Assert.All(invalidations, value => Assert.Single(value));
    }

    /// <summary>
    /// 单个键超过命令字节预算时拒绝批量删除。
    /// </summary>
    [Fact]
    public void RemoveBatch_RejectsSingleKeyOverBudget()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        var resources = new CacheResourceOptions(
            maxBatchCommandBytes: PairBytes("one") - 1);
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("budget-delete-single", "redis", resources: resources));

        var error = Assert.Throws<InvalidOperationException>(() => coordinator.RemoveBatch(new List<string> { "one" }));

        Assert.Contains("删除命令参数", error.Message);
        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Never);
    }

    /// <summary>
    /// Cluster 批量删除按槽位分组并使每组不超过命令预算。
    /// </summary>
    [Fact]
    public void ClusterRemoveBatch_PartitionsEachSlotWithinCommandBudget()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        database.Setup(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns(2L);
        var configuration = new CacheConfiguration("budget-cluster-delete", "redis",
            topology: CacheTopology.RedisCluster,
            resources: new CacheResourceOptions(maxBatchCommandBytes: 256));
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = Enumerable.Range(2, 100)
            .Select(index => CacheKeyFormat.GetPhysicalKey(configuration, "key-" + index))
            .First(key => CacheClusterTopology.GetHashSlot(key) != CacheClusterTopology.GetHashSlot(first));
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration);

        coordinator.RemoveBatch(new List<string> { first, second });

        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Exactly(2));
    }

    /// <summary>
    /// 异步批量写入使用一次批量命令并等待批量失效通知。
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_UsesOneBatchCommandAndAwaitsBatchInvalidation()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
                Task.FromResult(RedisResult.Create((RedisValue)(keys.Length / 2).ToString())));
        var invalidations = new List<string[]>();
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("batch-async", "redis"),
            invalidateBatchAsync: (keys, _) =>
            {
                invalidations.Add(keys.ToArray());
                return Task.CompletedTask;
            });

        await coordinator.WriteBatchAsync(new[]
        {
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2)
        }, TimeSpan.FromSeconds(1), CancellationToken.None);

        database.Verify(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
        Assert.Equal(new[] { "one", "two" }, invalidations.Single());
    }

    /// <summary>
    /// 异步批量删除遵守 Redis 命令字节预算。
    /// </summary>
    [Fact]
    public async Task RemoveBatchAsync_RespectsCommandByteBudget()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        database.Setup(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .ReturnsAsync(2L);
        var resources = new CacheResourceOptions(
            maxBatchCommandBytes: PairBytes("one"));
        var configuration = new CacheConfiguration("budget-delete-async", "redis", batchSize: 10,
            resources: resources);
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration);

        await coordinator.RemoveBatchAsync(new List<string> { "one", "two" }, CancellationToken.None);

        database.Verify(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Exactly(2));
    }

    /// <summary>
    /// Cluster 批量写入首个分组取消后停止派发后续分组。
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_CancellationAfterFirstClusterGroupStopsNextDispatch()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        var configuration = new CacheConfiguration("batch-cancel", "redis", maxConcurrency: 1,
            topology: CacheTopology.RedisCluster);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = Enumerable.Range(2, 100)
            .Select(index => CacheKeyFormat.GetPhysicalKey(configuration, "key-" + index))
            .First(key => CacheClusterTopology.GetHashSlot(key) != CacheClusterTopology.GetHashSlot(first));
        using var cancellation = new CancellationTokenSource();
        var issued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<RedisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] _, RedisValue[] _, CommandFlags _) =>
            {
                issued.SetResult(true);
                cancellation.Cancel();
                return pending.Task;
            });
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration);
        var write = coordinator.WriteBatchAsync(new[]
        {
            new KeyValuePair<string, int>(first, 1),
            new KeyValuePair<string, int>(second, 2)
        }, TimeSpan.FromSeconds(1), cancellation.Token);

        await issued.Task;
        pending.SetResult(RedisResult.Create((RedisValue)"1"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        database.Verify(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
    }

    /// <summary>
    /// Redis 写入提交后收到取消仍发送失效通知。
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancellationAfterRedisCommit_StillInvalidates()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        using var cancellation = new CancellationTokenSource();
        var observedToken = default(CancellationToken);
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] _, RedisValue[] _, CommandFlags _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(RedisResult.Create((RedisValue)"OK"));
            });
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("single-commit-cancel", "redis"),
            invalidateAsync: (_, token) =>
            {
                observedToken = token;
                return Task.CompletedTask;
            });

        var written = await coordinator.WriteAsync("one", 1, TimeSpan.FromSeconds(1), false,
            cancellation.Token);

        Assert.True(written);
        Assert.False(observedToken.IsCancellationRequested);
    }

    /// <summary>
    /// Redis 删除提交后收到取消仍发送失效通知。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_CancellationAfterRedisCommit_StillInvalidates()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        using var cancellation = new CancellationTokenSource();
        var observedToken = default(CancellationToken);
        database.Setup(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns((RedisKey[] _, CommandFlags _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(2L);
            });
        var coordinator = new RedisLeaseCoordinator(provider.Object,
            new CacheConfiguration("single-delete-commit-cancel", "redis"),
            invalidateAsync: (_, token) =>
            {
                observedToken = token;
                return Task.CompletedTask;
            });

        await coordinator.RemoveAsync("one", cancellation.Token);

        Assert.False(observedToken.IsCancellationRequested);
    }

    /// <summary>
    /// 批量写入首个提交后取消只使已提交批次失效。
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_CancellationAfterFirstCommit_InvalidatesCommittedBatchOnly()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        var configuration = new CacheConfiguration("batch-commit-cancel", "redis",
            topology: CacheTopology.RedisCluster, maxConcurrency: 1);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = Enumerable.Range(2, 100)
            .Select(index => CacheKeyFormat.GetPhysicalKey(configuration, "key-" + index))
            .First(key => CacheClusterTopology.GetHashSlot(key) != CacheClusterTopology.GetHashSlot(first));
        using var cancellation = new CancellationTokenSource();
        var invalidations = new List<string[]>();
        database.Setup(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns((string _, RedisKey[] keys, RedisValue[] _, CommandFlags _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(RedisResult.Create((RedisValue)(keys.Length / 2).ToString()));
            });
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration,
            invalidateBatchAsync: (keys, token) =>
            {
                Assert.False(token.IsCancellationRequested);
                invalidations.Add(keys.ToArray());
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.WriteBatchAsync(new[]
        {
            new KeyValuePair<string, int>(first, 1),
            new KeyValuePair<string, int>(second, 2)
        }, TimeSpan.FromSeconds(1), cancellation.Token));

        Assert.Single(invalidations);
        database.Verify(value => value.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(), CommandFlags.None), Times.Once);
    }

    /// <summary>
    /// 批量删除首个提交后取消只使已提交批次失效。
    /// </summary>
    [Fact]
    public async Task RemoveBatchAsync_CancellationAfterFirstCommit_InvalidatesCommittedBatchOnly()
    {
        var database = new Mock<IDatabase>();
        var provider = CreateProvider(database);
        var configuration = new CacheConfiguration("delete-commit-cancel", "redis",
            topology: CacheTopology.RedisCluster, maxConcurrency: 1);
        var first = CacheKeyFormat.GetPhysicalKey(configuration, "one");
        var second = Enumerable.Range(2, 100)
            .Select(index => CacheKeyFormat.GetPhysicalKey(configuration, "key-" + index))
            .First(key => CacheClusterTopology.GetHashSlot(key) != CacheClusterTopology.GetHashSlot(first));
        using var cancellation = new CancellationTokenSource();
        var invalidations = new List<string[]>();
        database.Setup(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None))
            .Returns((RedisKey[] _, CommandFlags _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(2L);
            });
        var coordinator = new RedisLeaseCoordinator(provider.Object, configuration,
            invalidateBatchAsync: (keys, token) =>
            {
                Assert.False(token.IsCancellationRequested);
                invalidations.Add(keys.ToArray());
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RemoveBatchAsync(
            new List<string> { first, second }, cancellation.Token));

        Assert.Single(invalidations);
        database.Verify(value => value.KeyDeleteAsync(It.IsAny<RedisKey[]>(), CommandFlags.None), Times.Once);
    }

    /// <summary>
    /// 创建绑定指定 Redis 数据库的 Provider 模拟对象。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <returns>返回已绑定数据库的 Provider 模拟对象。</returns>
    private static Mock<IEasyCachingProvider> CreateProvider(Mock<IDatabase> database)
    {
        var serializer = new Mock<IEasyCachingSerializer>();
        serializer.Setup(value => value.Serialize(It.IsAny<int>()))
            .Returns(new byte[] { 1 });
        var provider = new Mock<IEasyCachingProvider>();
        provider.SetupGet(value => value.Database).Returns(database.Object);
        provider.Setup(value => value.GetProviderInfo())
            .Returns(new ProviderInfo { Serializer = serializer.Object });
        return provider;
    }

    /// <summary>
    /// 计算物理键和值组成的 Redis 命令载荷字节数。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>返回物理键和值组成的命令载荷字节数。</returns>
    private static long PairBytes(string physicalKey) =>
        checked((long)Encoding.UTF8.GetByteCount(physicalKey) * 2 +
            Encoding.UTF8.GetByteCount(":lease"));
}
