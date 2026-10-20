using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 控制 Redis Cluster 集成测试是否执行。
/// </summary>
/// <remarks>
/// 只有启用 Provider 集成测试和 Cluster 测试门控时才执行。
/// </remarks>
public sealed class ClusterFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TESTS") != "true")
            Skip = "需启用隔离的 Redis Cluster 集成环境。";
    }
}

/// <summary>
/// 控制 Redis Cluster 大规模集成测试是否执行。
/// </summary>
/// <remarks>
/// 还需启用与数据规模对应的环境变量门控。
/// </remarks>
public sealed class ClusterLargeFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterLargeFactAttribute"/> 类型的实例。
    /// </summary>
    /// <param name="sizeGate">启用对应规模测试的环境变量名称。</param>
    public ClusterLargeFactAttribute(string sizeGate)
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TESTS") != "true" ||
            Environment.GetEnvironmentVariable(sizeGate) != "true")
            Skip = "需启用隔离的 Redis Cluster 大规模集成环境和对应规模门控。";
    }
}

/// <summary>
/// 验证 Redis Cluster 跨槽读取、扫描和清理能力。
/// </summary>
public class ClusterCacheTest
{
    /// <summary>
    /// 验证跨槽批量读取、前缀扫描和命名空间清理。
    /// </summary>
    [ClusterFact]
    public async Task ThreeMasters_CrossSlotReadScanAndClear()
    {
        var connections = new[] { "127.0.0.1:7000", "127.0.0.1:7001", "127.0.0.1:7002" };
        using var router = new CSRedisClient(connections[0]);
        var masters = connections.Select(value => new CSRedisClient(value)).ToArray();
        try
        {
            var configuration = new CacheConfiguration("cs-cluster-" + Guid.NewGuid().ToString("N"),
                "redis", topology: CacheTopology.RedisCluster, maxConcurrency: 4);
            var cache = new CSRedisCacheManager(router, configuration, new[] { router });
            for (var index = 0; index < 120; index++)
                await cache.SetAsync("item:" + index, index + 1);

            var inputKeys = Enumerable.Range(0, 120).Select(index => "item:" + index).ToArray();
            var syncValues = cache.GetAll<int>(inputKeys);
            Assert.Equal(Enumerable.Range(1, 120), syncValues);
            var values = await cache.GetAllAsync<int>(inputKeys);
            Assert.Equal(Enumerable.Range(1, 120), values);
            var streamed = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetAllStreamAsync<int>(inputKeys))
                streamed.Add(entry);
            Assert.Equal(inputKeys, streamed.Select(entry => entry.Key));
            Assert.All(streamed, entry => Assert.True(entry.Hit));
            Assert.Equal(Enumerable.Range(1, 120), streamed.Select(entry => entry.Value));
            Assert.Equal(120, (await cache.GetByPrefixAsync<int>("item:")).Count);
            Assert.Equal(120, cache.GetByPrefix<int>("item:").Count);
            var prefixStream = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>("item:"))
                prefixStream.Add(entry);
            Assert.Equal(120, prefixStream.Count);
            Assert.Equal(120, prefixStream.Select(entry => entry.Key).Distinct().Count());
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));
            Assert.Equal(121, await cache.GetAsync("leased", _ => Task.FromResult(121)));
            foreach (var node in router.Nodes.Values)
            {
                using var connection = node.Get();
                Assert.NotEmpty(connection.Value.Scan(0, CacheKeyFormat.GetNamespacePattern(configuration), 500).Items);
            }

            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int>("item:"));
        }
        finally
        {
            foreach (var master in masters) master.Dispose();
        }
    }

    /// <summary>
    /// 验证 10,000 条跨槽数据的列表和流式读取结果一致。
    /// </summary>
    [ClusterLargeFact("RUN_CACHE_CLUSTER_LARGE_10K_TESTS")]
    public Task TenThousandItems_CrossSlotListAndStreamRemainConsistent()
        => RunLargeConsistencyAsync(10_000);

    /// <summary>
    /// 验证 100,000 条跨槽数据的列表和流式读取结果一致。
    /// </summary>
    [ClusterLargeFact("RUN_CACHE_CLUSTER_LARGE_100K_TESTS")]
    public Task OneHundredThousandItems_CrossSlotListAndStreamRemainConsistent()
        => RunLargeConsistencyAsync(100_000);

    /// <summary>
    /// 执行或等待测试操作完成。
    /// </summary>
    /// <param name="itemCount">数据项数量。</param>
    private static async Task RunLargeConsistencyAsync(int itemCount)
    {
        var connections = new[] { "127.0.0.1:7000", "127.0.0.1:7001", "127.0.0.1:7002" };
        using var router = new CSRedisClient(connections[0]);
        var masters = new[] { router };
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("cs-cluster-large-" + suffix, "redis",
            topology: CacheTopology.RedisCluster, batchSize: 500, maxConcurrency: 8);
        var siblingConfiguration = new CacheConfiguration("cs-cluster-large-" + suffix + "-sibling",
            "redis", topology: CacheTopology.RedisCluster, batchSize: 500, maxConcurrency: 8);
        var cache = new CSRedisCacheManager(router, configuration, masters);
        var sibling = new CSRedisCacheManager(router, siblingConfiguration, masters);
        var prefix = "cluster-large:" + suffix + ":";
        var ordinaryKeys = Enumerable.Range(0, itemCount - 5)
            .Select(index => prefix + "item:" + index)
            .ToArray();
        var specialKeys = new[]
        {
            prefix + "Unicode:中文-😀",
            prefix + "花括号:{slot}",
            prefix + "星号:*",
            prefix + "问号:?",
            prefix + "反斜线:\\"
        };
        var allKeys = ordinaryKeys.Concat(specialKeys).ToArray();
        var values = allKeys.Select((key, index) =>
            new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var missingKeys = new[] { prefix + "missing:0", prefix + "missing:1" };
        var input = allKeys.ToList();
        var duplicateKey = allKeys[Math.Min(42, allKeys.Length - 1)];
        input.Insert(Math.Min(itemCount / 2, input.Count), duplicateKey);
        input.Insert(Math.Min(1_234, input.Count), missingKeys[0]);
        input.Add(missingKeys[1]);
        input.Add(specialKeys[^1]);
        var missingIndex = input.IndexOf(missingKeys[0]);
        var duplicateIndex = input.FindLastIndex(key => key == duplicateKey);
        var siblingKey = prefix + "sibling-only";

        Assert.True(GetHashSlots(CacheKeyFormat.GetPhysicalKeys(configuration, allKeys)).Count > 1,
            "大规模 Cluster 数据必须覆盖多个 Redis 槽位。");

        try
        {
            await cache.SetAllAsync(values);
            await sibling.SetAsync(siblingKey, 999_999);

            var expectedValues = input.Select(key =>
                values.TryGetValue(key, out var value) ? (int?)value : null).ToArray();
            var expectedHits = input.Select(values.ContainsKey).ToArray();
            var listValues = await cache.GetAllAsync<int?>(input);
            var streamed = new List<CacheEntry<int?>>(input.Count);
            await foreach (var entry in cache.GetAllStreamAsync<int?>(input))
                streamed.Add(entry);

            Assert.Equal(expectedValues, listValues);
            Assert.Equal(input, streamed.Select(entry => entry.Key));
            Assert.Equal(expectedValues, streamed.Select(entry => entry.Value));
            Assert.Equal(expectedHits, streamed.Select(entry => entry.Hit));
            Assert.False(streamed[missingIndex].Hit);
            Assert.Equal(duplicateKey, streamed[duplicateIndex].Key);
            Assert.True(streamed[^1].Hit);
            Assert.Equal(specialKeys[^1], streamed[^1].Key);

            var prefixValues = await cache.GetByPrefixAsync<int>(prefix);
            var prefixStreamed = new List<CacheEntry<int>>(allKeys.Length);
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                prefixStreamed.Add(entry);

            Assert.Equal(allKeys.Length, prefixValues.Count);
            Assert.Equal(allKeys.Length, prefixStreamed.Count);
            Assert.Equal(values.Values.OrderBy(value => value), prefixValues.OrderBy(value => value));

            var scannedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in prefixStreamed)
            {
                Assert.True(entry.Hit);
                Assert.True(values.TryGetValue(entry.Key, out var expected));
                Assert.Equal(expected, entry.Value);
                Assert.True(scannedKeys.Add(entry.Key), "Cluster 前缀扫描返回了重复逻辑键。");
            }

            Assert.True(scannedKeys.SetEquals(allKeys));
            Assert.DoesNotContain(prefixStreamed, entry => entry.Key == siblingKey);
            Assert.Equal(999_999, await sibling.GetAsync<int>(siblingKey));
        }
        finally
        {
            await cache.ClearAsync();
            await sibling.ClearAsync();
        }
    }

    /// <summary>
    /// 计算测试键的 Redis 哈希槽。
    /// </summary>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <returns>包含输入物理键哈希槽的集合。</returns>
    private static HashSet<int> GetHashSlots(IReadOnlyList<string> physicalKeys)
    {
        var slots = new HashSet<int>();
        foreach (var physicalKey in physicalKeys)
        {
            var start = physicalKey.IndexOf('{');
            var end = start < 0 ? -1 : physicalKey.IndexOf('}', start + 1);
            var key = start >= 0 && end > start + 1
                ? physicalKey.Substring(start + 1, end - start - 1)
                : physicalKey;
            var crc = 0;
            foreach (var value in System.Text.Encoding.UTF8.GetBytes(key))
            {
                crc ^= value << 8;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1;
                crc &= 0xffff;
            }
            slots.Add(crc % 16384);
        }
        return slots;
    }
}
