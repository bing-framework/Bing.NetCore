using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching Redis Cluster 集成测试门控。
/// </summary>
public sealed class EasyClusterFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyClusterFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要启用隔离的三主三从 Redis Cluster 集成环境。";
    }
}

/// <summary>
/// Redis Cluster 槽位归属变化测试门控。
/// </summary>
public sealed class EasyClusterSlotMigrationFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyClusterSlotMigrationFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterSlotMigrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_SLOT_MIGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要显式启用三主三从 Redis Cluster 和槽位归属变化集成测试。";
    }
}

/// <summary>
/// 仅在显式选择数据规模时运行 Redis Cluster 大规模功能一致性测试。
/// </summary>
public sealed class EasyClusterLargeRedisFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyClusterLargeRedisFactAttribute"/> 类型的实例。
    /// </summary>
    /// <param name="itemCount">数据项数量。</param>
    public EasyClusterLargeRedisFactAttribute(int itemCount)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要启用隔离的三主三从 Redis Cluster 集成环境。";
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_LARGE_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要显式设置 RUN_CACHE_CLUSTER_LARGE_TESTS=true。";
            return;
        }

        var configuredScale = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_CLUSTER_LARGE_SCALE")?.Trim();
        var expectedScale = itemCount.ToString(CultureInfo.InvariantCulture);
        var shortScale = itemCount == 10_000 ? "10k" : "100k";
        if (!string.Equals(configuredScale, expectedScale, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(configuredScale, shortScale, StringComparison.OrdinalIgnoreCase))
            Skip = $"需要显式设置 BING_TEST_EASYCACHING_CLUSTER_LARGE_SCALE={expectedScale}（或 {shortScale}）。";
    }
}

/// <summary>
/// 验证 Redis 和 Hybrid Cluster 的跨槽批量操作。
/// </summary>
public sealed class EasyClusterIntegrationTest
{
    /// <summary>
    /// Redis Cluster 测试使用的服务主机名。
    /// </summary>
    private const string Host = "127.0.0.1";
    /// <summary>
    /// Redis Cluster 测试使用的客户端节点端口。
    /// </summary>
    private static readonly int[] ClusterPorts = { 7000, 7001, 7002 };

    /// <summary>
    /// 保存 Redis Cluster 主节点及其槽位范围。
    /// </summary>
    private sealed class ClusterMaster
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterMaster"/> 类型的实例。
        /// </summary>
        /// <param name="id">Redis Cluster 节点标识。</param>
/// <param name="server">对应主节点的 Redis 服务接口。</param>
        /// <param name="slots">槽位集合。</param>
        public ClusterMaster(string id, IServer server, IReadOnlyList<(int Start, int End)> slots)
        {
            Id = id;
            Server = server;
            Slots = slots;
        }

        /// <summary>
        /// 获取节点标识。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 获取 Redis 服务端接口。
        /// </summary>
        public IServer Server { get; }

        /// <summary>
        /// 获取该测试节点负责的槽位范围集合。
        /// </summary>
        public IReadOnlyList<(int Start, int End)> Slots { get; }

        /// <summary>
        /// 判断节点是否负责指定槽位。
        /// </summary>
        /// <param name="slot">Redis 槽位。</param>
        /// <returns>如果当前节点负责指定槽位，则返回 true。</returns>
        public bool OwnsSlot(int slot) => Slots.Any(range => range.Start <= slot && slot <= range.End);
    }

    /// <summary>
    /// Redis Cluster 跨槽批量读取、流式读取、删除和清理保持命名空间隔离。
    /// </summary>
    [EasyClusterFact]
    public async Task RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation()
    {
        var configuration = CreateConfiguration("easy-cluster-redis-");
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var database = GetDatabase(container, configuration.ProviderName);
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName, topology: CacheTopology.RedisCluster);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");
        var prefix = "cluster:item:";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var values = keys.Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value);
        var requested = keys.Reverse().Take(80).Concat(new[] { keys[0], prefix + "missing" }).ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();

        AssertCrossSlot(physicalKeys);
        try
        {
            await database.StringSetAsync(sentinel, "outside");
            await cache.SetAllAsync(values);

            var list = await cache.GetAllAsync<int>(requested);
            var streamed = await ReadStreamAsync(cache.GetAllStreamAsync<int>(requested));
            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(requested.Select(key => values.TryGetValue(key, out var value) ? value : 0), list);
            Assert.Equal(list, streamed.Select(entry => entry.Value));
            Assert.Equal(requested.Select(values.ContainsKey), streamed.Select(entry => entry.Hit));

            var prefixList = await cache.GetByPrefixAsync<int>(prefix);
            var prefixStream = await ReadStreamAsync(cache.GetByPrefixStreamAsync<int>(prefix));
            Assert.Equal(values.Values.OrderBy(value => value), prefixList.OrderBy(value => value));
            Assert.Equal(prefixList.OrderBy(value => value), prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));

            await cache.RemoveAllAsync(keys.Take(20));
            var removed = await cache.GetAllAsync<int>(keys.Take(20));
            Assert.All(removed, value => Assert.Equal(0, value));
            var synchronousValues = keys.Take(20).ToDictionary(key => key, _ => 777);
            cache.SetAll(synchronousValues);
            Assert.All(cache.GetAll<int>(synchronousValues.Keys), value => Assert.Equal(777, value));
            cache.RemoveAll(synchronousValues.Keys);
            Assert.All(cache.GetAll<int>(synchronousValues.Keys), value => Assert.Equal(0, value));
            Assert.True(await database.KeyExistsAsync(sentinel));

            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int>(prefix));
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            await database.KeyDeleteAsync(sentinel);
        }
    }

    /// <summary>
    /// Hybrid Cluster 跨槽批量读取、流式读取、删除和清理同步分布式层与本地层。
    /// </summary>
    [EasyClusterFact]
    public async Task HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers()
    {
        var configuration = CreateConfiguration("easy-cluster-hybrid-");
        using var container = BuildHybrid(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var distributed = factory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var local = factory.GetCachingProvider(configuration.ProviderName + ":local");
        var database = Assert.IsAssignableFrom<IDatabase>(distributed.Database);
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName, topology: CacheTopology.RedisCluster);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");
        var prefix = "cluster:hybrid:item:";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var values = keys.Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value);
        var requested = keys.Where((_, index) => index % 2 == 0).Reverse()
            .Concat(new[] { keys[0], prefix + "missing" }).ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();

        AssertCrossSlot(physicalKeys);
        try
        {
            await database.StringSetAsync(sentinel, "outside");
            await cache.SetAllAsync(values);

            foreach (var key in physicalKeys)
                Assert.True(distributed.Get<int>(key).HasValue);
            Assert.All(physicalKeys, key => Assert.True(local.Get<int>(key).HasValue));

            var list = await cache.GetAllAsync<int>(requested);
            var streamed = await ReadStreamAsync(cache.GetAllStreamAsync<int>(requested));
            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(requested.Select(key => values.TryGetValue(key, out var value) ? value : 0), list);
            Assert.Equal(list, streamed.Select(entry => entry.Value));
            Assert.Equal(requested.Select(values.ContainsKey), streamed.Select(entry => entry.Hit));

            var prefixList = await cache.GetByPrefixAsync<int>(prefix);
            var prefixStream = await ReadStreamAsync(cache.GetByPrefixStreamAsync<int>(prefix));
            Assert.Equal(values.Values.OrderBy(value => value), prefixList.OrderBy(value => value));
            Assert.Equal(prefixList.OrderBy(value => value), prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));

            var removedKeys = keys.Take(20).ToArray();
            await cache.RemoveAllAsync(removedKeys);
            var removedPhysicalKeys = removedKeys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key));
            Assert.All(removedPhysicalKeys, key =>
            {
                Assert.False(local.Get<int>(key).HasValue);
                Assert.False(distributed.Get<int>(key).HasValue);
            });
            var synchronousValues = removedKeys.ToDictionary(key => key, _ => 777);
            cache.SetAll(synchronousValues);
            Assert.All(cache.GetAll<int>(synchronousValues.Keys), value => Assert.Equal(777, value));
            cache.RemoveAll(synchronousValues.Keys);
            Assert.All(removedPhysicalKeys, key =>
            {
                Assert.False(local.Get<int>(key).HasValue);
                Assert.False(distributed.Get<int>(key).HasValue);
            });
            Assert.True(await database.KeyExistsAsync(sentinel));

            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int>(prefix));
            Assert.All(physicalKeys, key => Assert.False(local.Get<int>(key).HasValue));
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            await database.KeyDeleteAsync(sentinel);
        }
    }

    /// <summary>
    /// Redis Cluster 死信管理在单槽内支持迁移、重放和删除。
    /// </summary>
    [EasyClusterFact]
    public async Task RedisCluster_DeadLetterHandlesMigrateReplayAndDeleteInOneSlot()
    {
        var configuration = CreateConfiguration("easy-cluster-outbox-");
        using var container = BuildRedis(configuration);
        var database = GetDatabase(container, configuration.ProviderName);
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var payloads = (RedisKey)(root + ":dead-payloads");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName + "-other", topology: CacheTopology.RedisCluster);
        var sentinel = (RedisKey)CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");
        using var first = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: 1_048_576);
        using var second = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: 1_048_576);
        try
        {
            await database.StringSetAsync(sentinel, "preserve");
            Assert.True(first.TryEnqueue("cluster-topic", new global::EasyCaching.Core.Bus.EasyCachingMessage
            {
                Id = "cluster-replay",
                CacheKeys = new[] { "cluster:key" }
            }));
            Assert.True(first.TryTake(out var replayItem));
            Assert.True(first.TryDeadLetter(replayItem));
            var replayRecord = Assert.Single(await first.ReadDeadLettersAsync(10,
                CancellationToken.None));

            Assert.Equal(replayItem.Payload, await second.GetDeadLetterPayloadAsync(
                replayRecord.Handle, CancellationToken.None));
            Assert.True(await second.ReplayDeadLetterAsync(replayRecord.Handle,
                CancellationToken.None));
            Assert.True(second.TryTake(out var replayed));
            Assert.True(second.TryDeadLetter(replayed));
            var deleteRecord = Assert.Single(await first.ReadDeadLettersAsync(10,
                CancellationToken.None));
            Assert.True(await first.DeleteDeadLetterAsync(deleteRecord.Handle,
                CancellationToken.None));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(0L, first.GetStatus().DeadLetterBytes);

            const string legacyPayload = "{\"Topic\":\"cluster-topic\",\"Id\":\"legacy\",\"CacheKeys\":[\"legacy:key\"],\"IsPrefix\":false,\"IsPattern\":false}";
            var legacyScore = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
            await database.SortedSetAddAsync(dead, legacyPayload, legacyScore);
            var legacyHandle = HybridInvalidationDeadLetterHandle.Create(legacyPayload);
            var legacyRecord = Assert.Single(await second.ReadDeadLettersAsync(10,
                CancellationToken.None));

            Assert.Equal(legacyHandle, legacyRecord.Handle);
            Assert.Equal(legacyPayload, await second.GetDeadLetterPayloadAsync(legacyHandle,
                CancellationToken.None));
            Assert.False((await database.SortedSetScoreAsync(dead, legacyPayload)).HasValue);
            Assert.Equal(legacyPayload, (await database.HashGetAsync(payloads, legacyHandle)).ToString());
            Assert.True(await second.DeleteDeadLetterAsync(legacyHandle, CancellationToken.None));
            Assert.Equal(0L, second.GetStatus().DeadLetterBytes);
            Assert.Equal("preserve", (await database.StringGetAsync(sentinel)).ToString());
        }
        finally
        {
            await database.KeyDeleteAsync(new[]
                { pending, processing, attempts, dead, payloads, sizes, bytes });
            await database.KeyDeleteAsync(sentinel);
        }
    }

    /// <summary>
    /// 验证 Redis Cluster 大规模读取一致性。
    /// </summary>
    /// <remarks>
    /// 覆盖 10,000 条数据的 List、流和前缀读取。
    /// </remarks>
    [EasyClusterLargeRedisFact(10_000)]
    public Task RedisCluster_10000ItemListStreamAndPrefixRemainConsistent() =>
        RunLargeClusterConsistencyAsync(10_000, hybrid: false);

    /// <summary>
    /// 验证 Redis Cluster 大规模读取一致性。
    /// </summary>
    /// <remarks>
    /// 覆盖 100,000 条数据的 List、流和前缀读取。
    /// </remarks>
    [EasyClusterLargeRedisFact(100_000)]
    public Task RedisCluster_100000ItemListStreamAndPrefixRemainConsistent() =>
        RunLargeClusterConsistencyAsync(100_000, hybrid: false);

    /// <summary>
    /// 验证 Hybrid Cluster 大规模读取一致性。
    /// </summary>
    /// <remarks>
    /// 覆盖 10,000 条数据的 List、流和前缀读取。
    /// </remarks>
    [EasyClusterLargeRedisFact(10_000)]
    public Task HybridCluster_10000ItemListStreamAndPrefixRemainConsistent() =>
        RunLargeClusterConsistencyAsync(10_000, hybrid: true);

    /// <summary>
    /// 验证 Hybrid Cluster 大规模读取一致性。
    /// </summary>
    /// <remarks>
    /// 覆盖 100,000 条数据的 List、流和前缀读取。
    /// </remarks>
    [EasyClusterLargeRedisFact(100_000)]
    public Task HybridCluster_100000ItemListStreamAndPrefixRemainConsistent() =>
        RunLargeClusterConsistencyAsync(100_000, hybrid: true);

    /// <summary>
    /// 执行指定规模的 Cluster 读取一致性检查。
    /// </summary>
    /// <remarks>
    /// 同时检查 Redis 或 Hybrid 的 List、流和前缀读取结果。
    /// </remarks>
    /// <param name="itemCount">数据项数量。</param>
    /// <param name="hybrid">是否使用 Hybrid Provider。</param>
    private static async Task RunLargeClusterConsistencyAsync(int itemCount, bool hybrid)
    {
        var configuration = new CacheConfiguration(
            (hybrid ? "easy-cluster-large-hybrid-" : "easy-cluster-large-") +
            Guid.NewGuid().ToString("N"),
            (hybrid ? "easy-cluster-large-hybrid-" : "easy-cluster-large-") +
            Guid.NewGuid().ToString("N"),
            batchSize: 500, maxConcurrency: 8, topology: CacheTopology.RedisCluster);
        using var container = hybrid ? BuildHybrid(configuration) : BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var database = hybrid
            ? Assert.IsAssignableFrom<IDatabase>(container.GetRequiredService<IEasyCachingProviderFactory>()
                .GetCachingProvider(configuration.ProviderName + ":distributed").Database)
            : GetDatabase(container, configuration.ProviderName);
        var prefix = "cluster:large:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, itemCount)
            .Select(index => prefix + index.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var requested = new List<string>(itemCount + 4)
        {
            prefix + "missing:first"
        };
        for (var index = 0; index < keys.Length; index++)
        {
            requested.Add(keys[index]);
            if (index == itemCount / 3)
                requested.Add(keys[index]);
            if (index == itemCount * 2 / 3)
                requested.Add(prefix + "missing:middle");
        }
        requested.Add(prefix + "missing:last");
        var expected = requested.Select(key =>
                values.TryGetValue(key, out var value) ? (int?)value : null)
            .ToArray();
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName, topology: CacheTopology.RedisCluster);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");

        try
        {
            await database.StringSetAsync(sentinel, "outside");
            await cache.SetAllAsync(values);

            var list = await cache.GetAllAsync<int?>(requested);
            Assert.Equal(expected, list);

            var streamed = await ReadStreamAsync(cache.GetAllStreamAsync<int?>(requested));
            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(expected, streamed.Select(entry => entry.Value));
            Assert.Equal(expected.Select(value => value.HasValue),
                streamed.Select(entry => entry.Hit));
            Assert.Equal(list, streamed.Select(entry => entry.Value));

            var prefixList = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStream = await ReadStreamAsync(cache.GetByPrefixStreamAsync<int?>(prefix));
            var expectedValues = values.Values.Select(value => (int?)value).OrderBy(value => value);
            Assert.Equal(itemCount, prefixList.Count);
            Assert.Equal(expectedValues, prefixList.OrderBy(value => value));
            Assert.Equal(prefixList.OrderBy(value => value),
                prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.Equal(itemCount, prefixStream.Count);
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            try
            {
                await cache.RemoveByPrefixAsync(prefix);
            }
            catch
            {
                // 保留主测试异常，同时尽量避免大规模测试数据残留。
            }

            try
            {
                await database.KeyDeleteAsync(sentinel);
            }
            catch
            {
                // 保留主测试异常。
            }
        }
    }

    /// <summary>
    /// Redis Cluster 槽位归属变化期间拒绝继续前缀流读取。
    /// </summary>
    [EasyClusterSlotMigrationFact]
    public async Task RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected()
    {
        var configuration = CreateConfiguration("easy-cluster-slot-change-");
        using var container = BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var database = GetDatabase(container, configuration.ProviderName);
        var prefix = "cluster:slot-change:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var values = keys.Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value);
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var excludedSlots = physicalKeys.Select(CacheClusterTopology.GetHashSlot).ToHashSet();
        var masters = await ReadClusterMastersAsync(database);
        var slot = await FindEmptySlotAsync(masters, excludedSlots);
        var source = masters.Single(master => master.OwnsSlot(slot));
        var destination = masters.First(master => master.Id != source.Id);
        Exception testError = null;
        var cleanupErrors = new List<Exception>();
        var slotChanged = false;

        try
        {
            AssertCrossSlot(physicalKeys);
            await cache.SetAllAsync(values);

            await using var stream = cache.GetByPrefixStreamAsync<int>(prefix).GetAsyncEnumerator();
            Assert.True(await stream.MoveNextAsync());
            Assert.True(stream.Current.Hit);

            slotChanged = true;
            await SetSlotOwnerAsync(masters, slot, destination.Id);

            var scanError = await Record.ExceptionAsync(async () =>
            {
                while (await stream.MoveNextAsync())
                {
                }
            });
            var topologyError = Assert.IsType<InvalidOperationException>(scanError);
            Assert.Contains("槽位归属发生变化", topologyError.Message, StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            if (slotChanged)
            {
                try
                {
                    await SetSlotOwnerAsync(masters, slot, source.Id);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }

            foreach (var physicalKey in physicalKeys)
            {
                try
                {
                    await database.KeyDeleteAsync(physicalKey);
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
        }

        if (testError != null && cleanupErrors.Count > 0)
            testError.Data["RedisClusterSlotMigrationCleanupError"] =
                new AggregateException(cleanupErrors).ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Redis Cluster 槽位归属变化测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 创建指定命名空间前缀的 Redis Cluster 配置。
    /// </summary>
    /// <param name="prefix">缓存键前缀。</param>
    /// <returns>返回配置了批量大小和 Cluster 拓扑的缓存配置。</returns>
    private static CacheConfiguration CreateConfiguration(string prefix) =>
        new(prefix + Guid.NewGuid().ToString("N"), "easy-cluster",
            batchSize: 16, maxConcurrency: 4, topology: CacheTopology.RedisCluster);

    /// <summary>
    /// 创建使用 Redis Cluster Provider 的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildRedis(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options => ConfigureRedis(options));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用 Hybrid Provider 的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            options => ConfigureRedis(options),
            bus => bus.ConfigurationOptions = CreateBusOptions());
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 设置测试 Redis Provider 的 Cluster 连接选项。
    /// </summary>
    /// <param name="options">测试选项。</param>
    private static void ConfigureRedis(global::EasyCaching.Redis.RedisOptions options)
    {
        foreach (var port in ClusterPorts)
            options.DBConfig.Endpoints.Add(new ServerEndPoint(Host, port));
    }

    /// <summary>
    /// 创建连接测试总线的 Redis 配置选项。
    /// </summary>
    /// <returns>返回测试总线使用的 Redis 连接选项。</returns>
    private static ConfigurationOptions CreateBusOptions()
    {
        var options = new ConfigurationOptions { AbortOnConnectFail = false };
        foreach (var port in ClusterPorts)
            options.EndPoints.Add(Host, port);
        return options;
    }

    /// <summary>
    /// 获取测试容器中指定 Provider 使用的 Redis 数据库。
    /// </summary>
    /// <param name="container">测试容器。</param>
    /// <param name="providerName">缓存 Provider 名称。</param>
    /// <returns>返回指定 Provider 使用的 Redis 数据库。</returns>
    private static IDatabase GetDatabase(ServiceProvider container, string providerName)
    {
        var provider = container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(providerName);
        return Assert.IsAssignableFrom<IDatabase>(provider.Database);
    }

    /// <summary>
    /// 读取 Redis Cluster 的主节点及其槽位范围。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <returns>包含各主节点及其槽位范围的异步任务。</returns>
    private static async Task<List<ClusterMaster>> ReadClusterMastersAsync(IDatabase database)
    {
        var servers = database.Multiplexer.GetEndPoints()
            .Select(endpoint => database.Multiplexer.GetServer(endpoint))
            .Where(server => !server.IsReplica && server.IsConnected)
            .ToArray();
        var seed = servers.FirstOrDefault() ?? throw new InvalidOperationException("Redis Cluster 没有可用主节点。");
        var nodes = (await seed.ExecuteAsync("CLUSTER", "NODES")).ToString();
        var masterIds = CacheClusterTopology.GetMasterIds(nodes);
        var serverById = new Dictionary<string, IServer>(StringComparer.Ordinal);
        foreach (var server in servers)
            serverById[server.Execute("CLUSTER", "MYID").ToString()] = server;

        var result = new List<ClusterMaster>(masterIds.Count);
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8 || !masterIds.Contains(fields[0]) || !serverById.TryGetValue(fields[0], out var server))
                continue;
            var slots = new List<(int Start, int End)>();
            for (var index = 8; index < fields.Length; index++)
            {
                if (fields[index].StartsWith("[", StringComparison.Ordinal))
                    continue;
                var range = fields[index].Split('-');
                var start = int.Parse(range[0], CultureInfo.InvariantCulture);
                var end = range.Length == 2 ? int.Parse(range[1], CultureInfo.InvariantCulture) : start;
                slots.Add((start, end));
            }
            result.Add(new ClusterMaster(fields[0], server, slots));
        }

        Assert.Equal(masterIds.Count, result.Count);
        return result;
    }

    /// <summary>
    /// 查找当前主节点未负责的 Redis 槽位。
    /// </summary>
    /// <returns>异步操作，完成后返回找到的空闲槽位编号。</returns>
    /// <param name="masters">主节点集合。</param>
    /// <param name="excludedSlots">排除的槽位集合。</param>
    private static async Task<int> FindEmptySlotAsync(IReadOnlyList<ClusterMaster> masters,
        IReadOnlySet<int> excludedSlots)
    {
        foreach (var master in masters)
        {
            foreach (var range in master.Slots)
            {
                for (var slot = range.Start; slot <= range.End; slot++)
                {
                    if (excludedSlots.Contains(slot))
                        continue;
                    var count = (long)await master.Server.ExecuteAsync("CLUSTER", "COUNTKEYSINSLOT", slot);
                    if (count == 0)
                        return slot;
                }
            }
        }

        throw new InvalidOperationException("Redis Cluster 没有可用于槽位归属变化测试的空槽位。");
    }

    /// <summary>
    /// 将指定槽位迁移到目标主节点。
    /// </summary>
    /// <param name="masters">主节点集合。</param>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="nodeId">目标主节点标识。</param>
    private static async Task SetSlotOwnerAsync(IEnumerable<ClusterMaster> masters, int slot, string nodeId)
    {
        foreach (var master in masters)
            await master.Server.ExecuteAsync("CLUSTER", "SETSLOT", slot, "NODE", nodeId);
    }

    /// <summary>
    /// 将缓存流中的条目物化为列表。
    /// </summary>
    /// <typeparam name="T">缓存条目值类型。</typeparam>
    /// <param name="stream">待读取的缓存流。</param>
    /// <returns>包含流中全部缓存条目的列表。</returns>
    private static async Task<List<CacheEntry<T>>> ReadStreamAsync<T>(
        IAsyncEnumerable<CacheEntry<T>> stream)
    {
        var result = new List<CacheEntry<T>>();
        await foreach (var entry in stream)
            result.Add(entry);
        return result;
    }

    /// <summary>
    /// 确认物理键分布在多个 Redis Cluster 槽位。
    /// </summary>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    private static void AssertCrossSlot(IEnumerable<string> physicalKeys)
    {
        Assert.True(physicalKeys.Select(CacheClusterTopology.GetHashSlot).Distinct().Count() > 1,
            "测试键必须覆盖至少两个 Redis Cluster 槽位。");
    }
}
