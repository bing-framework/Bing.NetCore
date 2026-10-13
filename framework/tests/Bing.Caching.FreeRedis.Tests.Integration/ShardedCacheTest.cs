using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 仅在配置第二个非生产 Redis 节点时运行客户端分片测试。
/// </summary>
public sealed class ShardedFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ShardedFactAttribute"/> 类型的实例。
    /// </summary>
    public ShardedFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")))
            Skip = "需启用 Redis 集成测试并设置第二个非生产节点连接串。";
    }
}

/// <summary>
/// 验证 FreeRedis 客户端分片的初始化、键路由和扫描。
/// </summary>
public class ShardedCacheTest
{
    /// <summary>
    /// 用于模拟不可用分片的 Redis 连接字符串。
    /// </summary>
    private const string UnavailableShardConnection =
        "127.0.0.1:1,connectTimeout=200,receiveTimeout=200,sendTimeout=200,retry=0,maxPoolSize=1,minPoolSize=1";

    /// <summary>
    /// 表示分片缓存测试中写入的序列化载荷。
    /// </summary>
    private sealed class Payload
    {
        /// <summary>
        /// 获取或设置序列化载荷中的数值。
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    /// 保存第一个客户端分片的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _first;

    /// <summary>
    /// 初始化一个 <see cref="ShardedCacheTest"/> 类型的实例。
    /// </summary>
    /// <param name="first">第一个客户端分片的 Redis 客户端。</param>
    public ShardedCacheTest(RedisClient first) => _first = first;

    /// <summary>
    /// 缺少预期在线分片时拒绝创建分片缓存管理器。
    /// </summary>
    [ShardedFact]
    public void OmittedOnlineShard_IsRejectedAtConstruction()
    {
        var configuration = new CacheConfiguration("free-shard-omitted-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        Assert.Throws<ArgumentException>(() => new FreeRedisCacheManager(_first, configuration, new[] { _first }));
    }

    /// <summary>
    /// 预期数量匹配但重复分片时仍拒绝创建管理器。
    /// </summary>
    [ShardedFact]
    public void RepeatedShard_IsRejectedEvenWhenExpectedCountMatches()
    {
        using var duplicate = CreateClient(Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection"));
        var configuration = new CacheConfiguration("free-shard-duplicate-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        Assert.Throws<InvalidOperationException>(() =>
            new FreeRedisCacheManager(_first, configuration, new[] { _first, duplicate }));
    }

    /// <summary>
    /// 在两个客户端分片间扫描数据并清理当前缓存命名空间。
    /// </summary>
    [ShardedFact]
    public async Task TwoShards_ScanAndClearAcrossBothNodes()
    {
        using var second = new RedisClient(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        second.Serialize = JsonConvert.SerializeObject;
        second.Deserialize = JsonConvert.DeserializeObject;
        var configuration = new CacheConfiguration("cache-sharded-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new FreeRedisCacheManager(_first, configuration, new[] { _first, second });
        for (var index = 0; index < 100; index++)
            await cache.SetAsync("item:" + index, index + 1);

        var firstCount = _first.Scan(CacheKeyFormat.GetNamespacePattern(configuration), 100, null)
            .Sum(page => page.Length);
        var secondCount = second.Scan(CacheKeyFormat.GetNamespacePattern(configuration), 100, null)
            .Sum(page => page.Length);
        Assert.True(firstCount > 0);
        Assert.True(secondCount > 0);
        Assert.Equal(100, (await cache.GetByPrefixAsync<int>("item:")).Count);
        await cache.ClearAsync();
        Assert.Empty(await cache.GetByPrefixAsync<int>("item:"));
    }

    /// <summary>
    /// 在两个客户端分片间批量写入和读取缓存项。
    /// </summary>
    [ShardedFact]
    public async Task TwoShards_BatchSetAndGetAcrossBothNodes()
    {
        using var second = CreateClient(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        var configuration = new CacheConfiguration("cache-sharded-batch-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new FreeRedisCacheManager(_first, configuration, new[] { _first, second });
        var items = CreateItems("batch-item:");

        await cache.SetAllAsync(items);

        var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
        Assert.True(_first.Scan(pattern, 100, null).Sum(page => page.Length) > 0);
        Assert.True(second.Scan(pattern, 100, null).Sum(page => page.Length) > 0);
        var values = await cache.GetAllAsync<int>(items.Keys);
        Assert.Equal(items.Keys.Select(key => items[key]), values);

        var orderedKeys = new[] { "batch-item:119", "batch-item:missing", "batch-item:0", "batch-item:119" };
        Assert.Equal(new[] { 120, 0, 1, 120 }, cache.GetAll<int>(orderedKeys));
        var prefixValues = cache.GetByPrefix<int>("batch-item:");
        Assert.Equal(items.Count, prefixValues.Count);
        Assert.Equal(items.Values.OrderBy(value => value), prefixValues.OrderBy(value => value));

        await cache.ClearAsync();
        Assert.Empty(await cache.GetByPrefixAsync<int>("batch-item:"));
    }

    /// <summary>
    /// 分片批量流和前缀流保持列表语义且不读取相邻命名空间。
    /// </summary>
    [ShardedFact]
    public async Task TwoShards_BatchStreamAndPrefixStreamRemainConsistent()
    {
        using var second = CreateClient(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("cache-sharded-stream-" + suffix,
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2, batchSize: 2);
        var siblingConfiguration = new CacheConfiguration("cache-sharded-stream-" + suffix + "-sibling",
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new FreeRedisCacheManager(_first, configuration, new[] { _first, second });
        var sibling = new FreeRedisCacheManager(_first, siblingConfiguration, new[] { _first, second });
        var prefix = "batch-stream:" + suffix + ":";
        var items = CreateItemsAcrossShards(configuration, prefix);
        var keys = items.Keys.ToArray();
        var nullKey = prefix + "null";
        var missingKey = prefix + "missing";
        var sentinelKey = prefix + "sentinel";
        var requested = new[] { keys[1], missingKey, nullKey, keys[0], keys[1], keys[^1] };

        try
        {
            await cache.SetAllAsync(items);
            await cache.SetAsync<int?>(nullKey, null);
            await sibling.SetAsync(sentinelKey, 99_999);

            var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
            Assert.True(_first.Scan(pattern, 100, null).Sum(page => page.Length) > 0);
            Assert.True(second.Scan(pattern, 100, null).Sum(page => page.Length) > 0);

            var expectedValues = requested.Select(key =>
                key == missingKey || key == nullKey ? (int?)null : items[key]).ToArray();
            var expectedHits = requested.Select(key => key != missingKey).ToArray();
            var listValues = await cache.GetAllAsync<int?>(requested);
            var streamed = new List<CacheEntry<int?>>(requested.Length);
            await foreach (var entry in cache.GetAllStreamAsync<int?>(requested))
                streamed.Add(entry);

            Assert.Equal(expectedValues, listValues);
            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(expectedValues, streamed.Select(entry => entry.Value));
            Assert.Equal(expectedHits, streamed.Select(entry => entry.Hit));

            var prefixValues = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStreamed = new List<CacheEntry<int?>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(prefix))
                prefixStreamed.Add(entry);

            var expectedPrefixKeys = keys.Append(nullKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            Assert.Equal(expectedPrefixKeys, prefixStreamed.Select(entry => entry.Key)
                .OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(prefixValues.OrderBy(value => value), prefixStreamed.Select(entry => entry.Value)
                .OrderBy(value => value));
            Assert.All(prefixStreamed, entry => Assert.True(entry.Hit));
            Assert.Contains(prefixStreamed, entry => entry.Key == nullKey && entry.Value == null);
            Assert.DoesNotContain(prefixStreamed, entry => entry.Key == sentinelKey);
            Assert.Equal(99_999, await sibling.GetAsync<int>(sentinelKey));
        }
        finally
        {
            await cache.ClearAsync();
            await sibling.ClearAsync();
        }
    }

    /// <summary>
    /// 分片不可用时，扫描读取和清理抛出错误而不返回局部成功。
    /// </summary>
    [ShardedFact]
    public async Task OneShardUnavailable_ScanBackedReadsAndClearThrowInsteadOfReturningPartialSuccess()
    {
        using var second = CreateClient(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        using var unavailable = CreateClient(UnavailableShardConnection);
        var configuration = new CacheConfiguration("cache-sharded-fault-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var healthyCache = new FreeRedisCacheManager(_first, configuration, new[] { _first, second });
        var items = CreateItems("fault-item:");

        try
        {
            await healthyCache.SetAllAsync(items);
            var pattern = CacheKeyFormat.GetNamespacePattern(configuration);
            Assert.True(_first.Scan(pattern, 100, null).Sum(page => page.Length) > 0);
            Assert.True(second.Scan(pattern, 100, null).Sum(page => page.Length) > 0);

            Assert.ThrowsAny<Exception>(() => new FreeRedisCacheManager(_first, configuration,
                new[] { _first, unavailable }));
            Assert.Equal(items.Count, (await healthyCache.GetByPrefixAsync<int>("fault-item:")).Count);
        }
        finally
        {
            await healthyCache.ClearAsync();
        }
    }

    /// <summary>
    /// 运行时类型读取使用目标分片配置的序列化器。
    /// </summary>
    [ShardedFact]
    public async Task RuntimeTypeRead_UsesSerializerFromRoutedShard()
    {
        using var second = new RedisClient(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION"));
        second.Serialize = value => "shard2:" + JsonConvert.SerializeObject(value);
        second.Deserialize = (json, type) =>
            JsonConvert.DeserializeObject(json.Substring("shard2:".Length), type);

        var configuration = new CacheConfiguration("cache-runtime-type-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var cache = new FreeRedisCacheManager(_first, configuration, new[] { _first, second });
        var key = Enumerable.Range(0, 1000)
            .Select(index => "runtime:" + index)
            .First(candidate =>
            {
                var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, candidate);
                var slotStart = physicalKey.IndexOf('{') + 1;
                return Convert.ToUInt32(physicalKey.Substring(slotStart, 8), 16) % 2 == 1;
            });

        await cache.SetAsync(key, new Payload { Value = 37 });

        Assert.Equal(37, cache.Get<Payload>(key).Value);
        Assert.Equal(37, ((Payload)await cache.GetAsync(key, typeof(Payload))).Value);

        await cache.RemoveAsync(key);
    }

    /// <summary>
    /// 创建连接到指定客户端分片的 Redis 客户端。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>连接到指定 Redis 节点的客户端。</returns>
    private static RedisClient CreateClient(string connection)
    {
        var client = new RedisClient(connection);
        client.Serialize = JsonConvert.SerializeObject;
        client.Deserialize = JsonConvert.DeserializeObject;
        return client;
    }

    /// <summary>
    /// 创建用于分片扫描测试的缓存项。
    /// </summary>
    /// <param name="prefix">缓存键前缀。</param>
    /// <param name="count">数量。</param>
    /// <returns>由指定前缀和数量生成的缓存项。</returns>
    private static Dictionary<string, int> CreateItems(string prefix, int count = 120) =>
        Enumerable.Range(0, count).ToDictionary(index => prefix + index, index => index + 1);

    /// <summary>
    /// 生成分布在两个分片上的测试缓存项。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="prefix">缓存键前缀。</param>
    /// <returns>覆盖两个分片的逻辑键及对应整数值。</returns>
    private static Dictionary<string, int> CreateItemsAcrossShards(CacheConfiguration configuration, string prefix)
    {
        var items = new Dictionary<string, int>(StringComparer.Ordinal);
        var shards = new bool[2];
        for (var index = 0; index < 10_000 && (!shards[0] || !shards[1] || items.Count < 8); index++)
        {
            var key = prefix + index;
            var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);
            var slotStart = physicalKey.IndexOf('{') + 1;
            var shard = (int)(Convert.ToUInt32(physicalKey.Substring(slotStart, 8), 16) % 2);
            shards[shard] = true;
            items.Add(key, index + 1);
        }

        Assert.True(shards[0]);
        Assert.True(shards[1]);
        return items;
    }
}
