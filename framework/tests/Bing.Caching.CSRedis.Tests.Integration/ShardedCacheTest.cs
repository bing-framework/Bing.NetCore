using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// Redis 客户端分片测试特性。
/// </summary>
/// <remarks>
/// 仅在启用 Redis 集成测试并配置第二个分片连接串时运行测试。
/// </remarks>
public sealed class ShardedFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ShardedFactAttribute"/> 类型的实例。
    /// </summary>
    /// <remarks>未启用 Redis 集成测试或未配置第二个分片连接串时设置跳过原因。</remarks>
    public ShardedFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")))
            Skip = "需启用 Redis 集成测试并设置第二个非生产节点连接串。";
    }
}

/// <summary>
/// CSRedis 客户端分片测试。
/// </summary>
/// <remarks>
/// 覆盖分片路由、批量读写、扫描清理和不可用节点处理。
/// </remarks>
public class ShardedCacheTest
{
    /// <summary>
    /// 不可用分片模拟连接串。
    /// </summary>
    /// <remarks>
    /// 连接指向本地未监听端口，并设置短连接超时以快速模拟分片不可用。
    /// </remarks>
    private const string UnavailableShardConnection =
        "127.0.0.1:1,connectTimeout=200,poolsize=1";

    /// <summary>
    /// 验证缺少分片节点时拒绝配置。
    /// </summary>
    /// <remarks>
    /// 在线分片数量少于 expectedShardCount 时，缓存管理器应在构造阶段抛出异常。
    /// </remarks>
    [ShardedFact]
    public void OmittedOnlineShard_IsRejectedAtConstruction()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        using var client = new CSRedisClient(connection);
        var configuration = new CacheConfiguration("cs-shard-omitted-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        Assert.Throws<ArgumentException>(() => new CSRedisCacheManager(client, configuration, new[] { client }));
    }

    /// <summary>
    /// 验证重复分片节点被拒绝。
    /// </summary>
    /// <remarks>
    /// 即使分片数量达到 expectedShardCount，也不得接受引用同一节点的配置。
    /// </remarks>
    [ShardedFact]
    public void RepeatedShard_IsRejectedEvenWhenExpectedCountMatches()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        using var first = new CSRedisClient(connection);
        using var duplicate = new CSRedisClient(connection);
        var configuration = new CacheConfiguration("cs-shard-duplicate-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        Assert.Throws<InvalidOperationException>(() =>
            new CSRedisCacheManager(first, configuration, new[] { first, duplicate }));
    }

    /// <summary>
    /// 验证双分片扫描和清理。
    /// </summary>
    /// <remarks>
    /// 前缀扫描应遍历两个节点，清理后不应返回残留数据。
    /// </remarks>
    [ShardedFact]
    public async Task TwoShards_ScanAndClearAcrossBothNodes()
    {
        var firstConnection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        using var first = new CSRedisClient(firstConnection);
        using var second = new CSRedisClient(secondConnection);
        using var router = new CSRedisClient(null, new[] { firstConnection, secondConnection });
        var configuration = new CacheConfiguration("csredis-sharded-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new CSRedisCacheManager(router, configuration, new[] { first, second });
        for (var index = 0; index < 100; index++)
            await cache.SetAsync("item:" + index, index + 1);

        var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
        Assert.NotEmpty(first.Scan(0, pattern, 500).Items);
        Assert.NotEmpty(second.Scan(0, pattern, 500).Items);
        Assert.Equal(100, (await cache.GetByPrefixAsync<int>("item:")).Count);
        await cache.ClearAsync();
        Assert.Empty(await cache.GetByPrefixAsync<int>("item:"));
    }

    /// <summary>
    /// 验证双分片批量读写结果一致。
    /// </summary>
    /// <remarks>
    /// 同时覆盖批量写入、批量读取、按前缀读取和按序流式读取。
    /// </remarks>
    [ShardedFact]
    public async Task TwoShards_BatchSetAndGetAcrossBothNodes()
    {
        var firstConnection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        using var first = CreateClient(firstConnection);
        using var second = CreateClient(secondConnection);
        using var router = new CSRedisClient(null, new[] { firstConnection, secondConnection });
        var configuration = new CacheConfiguration("csredis-sharded-batch-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new CSRedisCacheManager(router, configuration, new[] { first, second });
        var items = CreateItems("batch-item:");

        await cache.SetAllAsync(items);

        var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
        Assert.NotEmpty(first.Scan(0, pattern, 500).Items);
        Assert.NotEmpty(second.Scan(0, pattern, 500).Items);
        var values = await cache.GetAllAsync<int>(items.Keys);
        Assert.Equal(items.Keys.Select(key => items[key]), values);

        var syncOrderedKeys = new[] { "batch-item:119", "batch-item:missing", "batch-item:0", "batch-item:119" };
        Assert.Equal(new[] { 120, 0, 1, 120 }, cache.GetAll<int>(syncOrderedKeys));
        var prefixValues = cache.GetByPrefix<int>("batch-item:");
        Assert.Equal(items.Count, prefixValues.Count);
        Assert.Equal(items.Values.OrderBy(value => value), prefixValues.OrderBy(value => value));

        var orderedKeys = new[] { "batch-item:119", "missing", "batch-item:0", "batch-item:119" };
        var streamed = new List<CacheEntry<int>>();
        await foreach (var entry in cache.GetAllStreamAsync<int>(orderedKeys))
            streamed.Add(entry);
        Assert.Equal(orderedKeys, streamed.Select(entry => entry.Key));
        Assert.Equal(new[] { 120, 0, 1, 120 }, streamed.Select(entry => entry.Value));
        Assert.Equal(new[] { true, false, true, true }, streamed.Select(entry => entry.Hit));

        await cache.ClearAsync();
        Assert.Empty(await cache.GetByPrefixAsync<int>("batch-item:"));
    }

    /// <summary>
    /// 验证双分片前缀列表和流式读取一致。
    /// </summary>
    /// <remarks>
    /// 检查读取结果、命中状态及兄弟命名空间隔离。
    /// </remarks>
    [ShardedFact]
    public async Task TwoShards_PrefixListAndStreamRemainConsistentAcrossBothNodes()
    {
        var firstConnection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        using var first = CreateClient(firstConnection);
        using var second = CreateClient(secondConnection);
        using var router = new CSRedisClient(null, new[] { firstConnection, secondConnection });
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("csredis-sharded-stream-" + suffix, "redis",
            batchSize: 2, topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var siblingConfiguration = new CacheConfiguration(configuration.Namespace + "-sibling",
            configuration.ProviderName, topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new CSRedisCacheManager(router, configuration, new[] { first, second });
        var sibling = new CSRedisCacheManager(router, siblingConfiguration, new[] { first, second });
        var prefix = "stream-item:" + suffix + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var nullKey = prefix + "null";
        var missingKey = prefix + "missing";
        var requested = keys.Reverse().Take(80)
            .Concat(new[] { keys[0], nullKey, missingKey, keys[0] }).ToArray();

        try
        {
            await cache.SetAllAsync(values);
            await cache.SetAsync<int?>(nullKey, null);
            await sibling.SetAsync(prefix + "sentinel", 999_999);

            var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
            Assert.NotEmpty(first.Scan(0, pattern, 500).Items);
            Assert.NotEmpty(second.Scan(0, pattern, 500).Items);

            var list = await cache.GetAllAsync<int?>(requested);
            var streamed = new List<CacheEntry<int?>>(requested.Length);
            await foreach (var entry in cache.GetAllStreamAsync<int?>(requested))
                streamed.Add(entry);

            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(list, streamed.Select(entry => entry.Value));
            Assert.Equal(requested.Select(key => values.ContainsKey(key) || key == nullKey),
                streamed.Select(entry => entry.Hit));
            Assert.True(streamed[requested.Length - 3].Hit);
            Assert.Null(streamed[requested.Length - 3].Value);
            Assert.False(streamed[requested.Length - 2].Hit);
            Assert.True(streamed[^1].Hit);

            var prefixList = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStream = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(prefix))
                prefixStream.Add(entry);

            Assert.Equal(values.Count + 1, prefixList.Count);
            Assert.Equal(prefixList.OrderBy(value => value),
                prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.Equal(values.Keys.Append(nullKey).OrderBy(key => key),
                prefixStream.Select(entry => entry.Key).OrderBy(key => key));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
            Assert.DoesNotContain(prefixStream, entry => entry.Key == prefix + "sentinel");
            Assert.Equal(999_999, await sibling.GetAsync<int>(prefix + "sentinel"));
        }
        finally
        {
            await cache.ClearAsync();
            await sibling.ClearAsync();
        }
    }

    /// <summary>
    /// 验证自定义节点规则的分片映射。
    /// </summary>
    /// <remarks>
    /// 流式批量读取时，物理键应根据自定义规则映射到正确分片。
    /// </remarks>
    [ShardedFact]
    public async Task CustomNodeRule_UsesMatchingShardMappingForBatchRead()
    {
        var firstConnection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        using var first = CreateClient(firstConnection);
        using var second = CreateClient(secondConnection);
        var connections = new[] { firstConnection, secondConnection };
        using var defaultRouter = new CSRedisClient(null, connections);
        var nodeIds = Enumerable.Range(0, defaultRouter.NodesIndex.Count)
            .Select(index => defaultRouter.NodesIndex[index]).ToArray();
        int Index(string physicalKey) => physicalKey.EndsWith("first", StringComparison.Ordinal) ? 0 : 1;
        using var router = new CSRedisClient(key => nodeIds[Index(key)], connections);
        var configuration = new CacheConfiguration("csredis-custom-route-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new CSRedisCacheManager(router, configuration, new[] { first, second }, Index);

        try
        {
            await cache.SetAsync("custom:first", 11);
            await cache.SetAsync("custom:second", 22);
            Assert.NotNull(first.Get(CacheKeyFormat.GetPhysicalKey(configuration, "custom:first")));
            Assert.NotNull(second.Get(CacheKeyFormat.GetPhysicalKey(configuration, "custom:second")));

            var entries = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetAllStreamAsync<int>(new[]
                           { "custom:second", "missing", "custom:first", "custom:second" }))
                entries.Add(entry);
            Assert.Equal(new[] { 22, 0, 11, 22 }, entries.Select(entry => entry.Value));
            Assert.Equal(new[] { true, false, true, true }, entries.Select(entry => entry.Hit));
        }
        finally
        {
            await cache.ClearAsync();
        }
    }

    /// <summary>
    /// 验证不可用分片不会产生部分成功。
    /// </summary>
    /// <remarks>
    /// 含不可用分片的管理器构造应失败，健康管理器的跨分片读取应保持结果完整。
    /// </remarks>
    [ShardedFact]
    public async Task OneShardUnavailable_ScanBackedReadsAndClearThrowInsteadOfReturningPartialSuccess()
    {
        var firstConnection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        using var first = CreateClient(firstConnection);
        using var second = CreateClient(secondConnection);
        using var unavailable = CreateClient(UnavailableShardConnection);
        using var router = new CSRedisClient(null, new[] { firstConnection, secondConnection });
        var configuration = new CacheConfiguration("csredis-sharded-fault-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var healthyCache = new CSRedisCacheManager(router, configuration, new[] { first, second });
        var items = CreateItems("fault-item:");

        try
        {
            await healthyCache.SetAllAsync(items);
            var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
            Assert.NotEmpty(first.Scan(0, pattern, 500).Items);
            Assert.NotEmpty(second.Scan(0, pattern, 500).Items);

            Assert.ThrowsAny<Exception>(() => new CSRedisCacheManager(router, configuration,
                new[] { first, unavailable }));
            Assert.Equal(items.Count, (await healthyCache.GetByPrefixAsync<int>("fault-item:")).Count);
        }
        finally
        {
            await healthyCache.ClearAsync();
        }
    }

    /// <summary>
    /// 创建单节点 CSRedis 客户端。
    /// </summary>
    /// <param name="connection">目标 Redis 节点的连接串。</param>
    /// <returns>连接到指定节点的 CSRedis 客户端。</returns>
    private static CSRedisClient CreateClient(string connection) => new CSRedisClient(connection);

    /// <summary>
    /// 生成顺序整数测试项。
    /// </summary>
    /// <remarks>
    /// 键使用指定前缀，值从 1 开始按生成顺序递增。
    /// </remarks>
    /// <param name="prefix">逻辑缓存键使用的前缀。</param>
    /// <param name="count">要生成的测试项数量。</param>
    /// <returns>以逻辑键为键、以从 1 开始的顺序整数为值的字典。</returns>
    private static Dictionary<string, int> CreateItems(string prefix, int count = 120) =>
        Enumerable.Range(0, count).ToDictionary(index => prefix + index, index => index + 1);
}
