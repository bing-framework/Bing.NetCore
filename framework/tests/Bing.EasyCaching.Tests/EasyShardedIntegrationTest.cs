using Bing.Caching;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Globalization;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching 客户端分片集成测试门控。
/// </summary>
public sealed class EasyShardedRedisFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedRedisFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedRedisFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")))
            Skip = "需要启用 Redis 集成测试并设置两个独立的非生产 Redis 节点。";
    }
}

/// <summary>
/// 仅在显式选择数据规模时运行双分片大规模批量一致性测试。
/// </summary>
public sealed class EasyShardedLargeRedisFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedLargeRedisFactAttribute"/> 类型的实例。
    /// </summary>
    /// <param name="itemCount">数据项数量。</param>
    public EasyShardedLargeRedisFactAttribute(int itemCount)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")))
        {
            Skip = "需要启用 Redis 集成测试并设置两个独立的非生产 Redis 节点。";
            return;
        }

        var configuredScale = Environment.GetEnvironmentVariable(
            "BING_TEST_EASYCACHING_SHARDED_LARGE_SCALE")?.Trim();
        var expectedScale = itemCount.ToString(CultureInfo.InvariantCulture);
        var shortScale = itemCount == 10_000 ? "10k" : "100k";
        if (!string.Equals(configuredScale, expectedScale, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(configuredScale, shortScale, StringComparison.OrdinalIgnoreCase))
            Skip = $"需要显式设置 BING_TEST_EASYCACHING_SHARDED_LARGE_SCALE={expectedScale}（或 {shortScale}）。";
    }
}

/// <summary>
/// 验证客户端分片适配器的跨节点路由、扫描和配置校验。
/// </summary>
public sealed class EasyShardedIntegrationTest
{
    /// <summary>
    /// 客户端分片在多个节点间共享资源准入和载荷预算。
    /// </summary>
    [EasyShardedRedisFact]
    public async Task ShardedRedis_SharesFactoryAdmissionAndPayloadBudgetAcrossNodes()
    {
        var configuration = new CacheConfiguration("shard-resource-" + Guid.NewGuid().ToString("N"),
            "shard-resource", batchSize: 2, topology: CacheTopology.ClientSharded, expectedShardCount: 2,
            resources: new CacheResourceOptions(1, 8, 32));
        using var container = BuildCache(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var keys = CreateKeys(configuration, "resource:").Take(2)
            .Concat(Enumerable.Range(0, 3).Select(_ => "resource:" + Guid.NewGuid().ToString("N"))).ToArray();
        Assert.NotEqual(GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, keys[0])),
            GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, keys[1])));
        await Bing.Caching.ContractTests.CacheCoordinationAssertions.VerifyResourceLimitsAsync(cache, keys: keys);
    }

    /// <summary>
    /// 客户端分片跨节点完成批量读写、流式读取、前缀扫描、清理和回源。
    /// </summary>
    [EasyShardedRedisFact]
    public async Task ShardedRedis_CrossNodeBatchStreamScanClearAndRetriever_Work()
    {
        var configuration = new CacheConfiguration(
            "easy-sharded-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var container = BuildCache(configuration);
        using var firstConnection = ConnectionMultiplexer.Connect(FirstConnection);
        using var secondConnection = ConnectionMultiplexer.Connect(SecondConnection);
        var firstDatabase = firstConnection.GetDatabase();
        var secondDatabase = secondConnection.GetDatabase();
        var cache = container.GetRequiredService<IRedisCache>();
        var prefix = "item:";
        var keys = CreateKeys(configuration, prefix);
        var requested = new[] { keys[1], keys[0], prefix + "missing", keys[1] };
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value);
        var otherConfiguration = new CacheConfiguration(
            configuration.Namespace + "-other", configuration.ProviderName,
            topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, prefix + "sentinel");

        try
        {
            await firstDatabase.KeyDeleteAsync(sentinel);
            await secondDatabase.KeyDeleteAsync(sentinel);
            await firstDatabase.StringSetAsync(sentinel, "outside");

            await cache.SetAsync("single:first", 101);
            await cache.SetAsync("single:second", 202);
            Assert.Equal(101, await cache.GetAsync<int>("single:first"));
            Assert.Equal(202, await cache.GetAsync<int>("single:second"));

            await cache.SetAllAsync(values);
            var physicalKeys = values.Keys.Select(key =>
                CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
            var firstNodeCount = await CountExistingAsync(firstDatabase, physicalKeys);
            var secondNodeCount = await CountExistingAsync(secondDatabase, physicalKeys);
            Assert.True(firstNodeCount > 0, "至少一个分片键应写入第一个 Redis 节点。");
            Assert.True(secondNodeCount > 0, "至少一个分片键应写入第二个 Redis 节点。");

            var list = await cache.GetAllAsync<int?>(requested);
            var stream = await ReadStreamAsync(cache.GetAllStreamAsync<int?>(requested));
            Assert.Equal(requested, stream.Select(entry => entry.Key));
            Assert.Equal(new int?[] { values[keys[1]], values[keys[0]], null, values[keys[1]] }, list);
            Assert.Equal(list, stream.Select(entry => entry.Value));
            Assert.Equal(new[] { true, true, false, true }, stream.Select(entry => entry.Hit));

            var prefixList = await cache.GetByPrefixAsync<int?>(prefix);
            var prefixStream = await ReadStreamAsync(cache.GetByPrefixStreamAsync<int?>(prefix));
            Assert.Equal(values.Values.Select(value => (int?)value).OrderBy(value => value),
                prefixList.OrderBy(value => value));
            Assert.Equal(prefixList.OrderBy(value => value), prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));

            var backfilled = await cache.GetAsync("backfilled", _ => Task.FromResult(404));
            Assert.Equal(404, backfilled);
            Assert.Equal(404, await cache.GetAsync<int>("backfilled"));

            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int?>(prefix));
            Assert.False(await cache.ExistsAsync("single:first"));
            Assert.False(await cache.ExistsAsync("single:second"));
            Assert.True(await firstDatabase.KeyExistsAsync(sentinel));
            Assert.False(await secondDatabase.KeyExistsAsync(sentinel));
        }
        finally
        {
            await cache.ClearAsync();
            await firstDatabase.KeyDeleteAsync(sentinel);
            await secondDatabase.KeyDeleteAsync(sentinel);
        }
    }

    /// <summary>
    /// 两分片在 10,000 条数据下保持列表和流式读取一致。
    /// </summary>
    [EasyShardedLargeRedisFact(10_000)]
    public Task ShardedRedis_10000ItemListAndStreamRemainConsistentAcrossShards() =>
        RunLargeListAndStreamConsistencyAsync(10_000);

    /// <summary>
    /// 两分片在 100,000 条数据下保持列表和流式读取一致。
    /// </summary>
    [EasyShardedLargeRedisFact(100_000)]
    public Task ShardedRedis_100000ItemListAndStreamRemainConsistentAcrossShards() =>
        RunLargeListAndStreamConsistencyAsync(100_000);

    /// <summary>
    /// 验证大批量列表读取与流式读取一致。
    /// </summary>
    /// <param name="itemCount">数据项数量。</param>
    private static async Task RunLargeListAndStreamConsistencyAsync(int itemCount)
    {
        var configuration = new CacheConfiguration(
            "easy-sharded-large-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-large-" + Guid.NewGuid().ToString("N"),
            batchSize: 500,
            maxConcurrency: 8,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var container = BuildCache(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var prefix = "large:" + Guid.NewGuid().ToString("N") + ":";
        var targetShardZeroCount = itemCount / 2;
        var targetShardOneCount = itemCount - targetShardZeroCount;
        var shardZeroKeys = new List<string>(targetShardZeroCount);
        var shardOneKeys = new List<string>(targetShardOneCount);

        for (var index = 0;
             (shardZeroKeys.Count < targetShardZeroCount || shardOneKeys.Count < targetShardOneCount) &&
             index < itemCount * 20;
             index++)
        {
            var key = prefix + index.ToString(CultureInfo.InvariantCulture);
            if (GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, key)) == 0)
            {
                if (shardZeroKeys.Count < targetShardZeroCount)
                    shardZeroKeys.Add(key);
            }
            else if (shardOneKeys.Count < targetShardOneCount)
            {
                shardOneKeys.Add(key);
            }
        }

        Assert.Equal(targetShardZeroCount, shardZeroKeys.Count);
        Assert.Equal(targetShardOneCount, shardOneKeys.Count);

        var orderedKeys = new List<string>(itemCount);
        for (var index = 0; index < Math.Max(shardZeroKeys.Count, shardOneKeys.Count); index++)
        {
            if (index < shardZeroKeys.Count)
                orderedKeys.Add(shardZeroKeys[index]);
            if (index < shardOneKeys.Count)
                orderedKeys.Add(shardOneKeys[index]);
        }

        Assert.Equal(itemCount, orderedKeys.Count);
        Assert.Contains(orderedKeys, key =>
            GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, key)) == 0);
        Assert.Contains(orderedKeys, key =>
            GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, key)) == 1);

        var values = orderedKeys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var requested = new List<string>(itemCount + 4)
        {
            prefix + "missing:first"
        };
        for (var index = 0; index < orderedKeys.Count; index++)
        {
            requested.Add(orderedKeys[index]);
            if (index == itemCount / 3)
                requested.Add(orderedKeys[index]);
            if (index == itemCount * 2 / 3)
                requested.Add(prefix + "missing:middle");
        }
        requested.Add(prefix + "missing:last");

        var expected = requested.Select(key =>
                values.TryGetValue(key, out var value) ? (int?)value : null)
            .ToArray();

        try
        {
            await cache.SetAllAsync(values);

            var list = await cache.GetAllAsync<int?>(requested);
            Assert.Equal(expected, list);

            var stream = await ReadStreamAsync(cache.GetAllStreamAsync<int?>(requested));
            Assert.Equal(requested, stream.Select(entry => entry.Key));
            Assert.Equal(expected, stream.Select(entry => entry.Value));
            Assert.Equal(expected.Select(value => value.HasValue), stream.Select(entry => entry.Hit));
            Assert.Equal(list, stream.Select(entry => entry.Value));
        }
        finally
        {
            try
            {
                await cache.ClearAsync();
            }
            catch
            {
                // 保留主测试异常，同时尽量避免大规模测试数据残留。
            }
        }
    }

    /// <summary>
    /// 缺少分片配置时在注册 Provider 前拒绝。
    /// </summary>
    [EasyShardedRedisFact]
    public void MissingShardConfiguration_IsRejectedBeforeProviderRegistration()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddShardedRedisCaching(configuration,
            new[] { ConfigureNode(FirstConnection) }));
        Assert.Empty(services);
    }

    /// <summary>
    /// 解析客户端分片管理器时拒绝重复的分片身份。
    /// </summary>
    [EasyShardedRedisFact]
    public void DuplicateShardIdentity_IsRejectedWhenManagerIsResolved()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedRedisCaching(configuration, new[]
        {
            ConfigureNode(FirstConnection),
            ConfigureNode(FirstConnection)
        });

        using var container = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() =>
            container.GetRequiredService<IRedisCache>());
    }

    /// <summary>
    /// 创建用于验证分片配置边界的缓存配置。
    /// </summary>
    /// <returns>返回配置了两个客户端分片的缓存配置。</returns>
    private static CacheConfiguration CreateConfiguration() => new(
        "easy-sharded-invalid-" + Guid.NewGuid().ToString("N"),
        "easy-sharded-invalid-" + Guid.NewGuid().ToString("N"),
        topology: CacheTopology.ClientSharded, expectedShardCount: 2);

    /// <summary>
    /// 创建绑定两个 Redis 分片的客户端分片服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册客户端分片 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildCache(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedRedisCaching(configuration, new[]
        {
            ConfigureNode(FirstConnection),
            ConfigureNode(SecondConnection)
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 为指定 Redis 节点创建连接配置回调。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回应用 Redis 节点连接设置的配置回调。</returns>
    private static Action<RedisOptions> ConfigureNode(string connection) => options =>
    {
        var endpoint = ParseEndpoint(connection);
        options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port));
        options.DBConfig.AbortOnConnectFail = false;
    };

    /// <summary>
    /// 解析测试 Redis 连接字符串中的端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        var endpoint = connection.Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1 ||
            !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new FormatException("Redis 连接串必须包含 host:port 端点。");
        var host = endpoint[..separator].Trim();
        if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
            host = host[1..^1];
        return (host, port);
    }

    /// <summary>
    /// 生成覆盖两个客户端分片的测试键。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="prefix">缓存键前缀。</param>
    /// <returns>返回覆盖两个分片的逻辑键集合。</returns>
    private static string[] CreateKeys(CacheConfiguration configuration, string prefix)
    {
        var byShard = new string[2];
        var candidates = new List<string>();
        for (var index = 0; index < 10000 && byShard.Any(key => key == null); index++)
        {
            var key = prefix + index;
            candidates.Add(key);
            var shard = GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, key));
            if (byShard[shard] == null)
                byShard[shard] = key;
        }

        Assert.All(byShard, key => Assert.NotNull(key));
        return byShard.Concat(candidates).Distinct(StringComparer.Ordinal).Take(64).ToArray();
    }

    /// <summary>
    /// 根据物理键中的哈希标签计算客户端分片索引。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>返回物理键所属的分片索引。</returns>
    private static int GetExpectedShard(string physicalKey)
    {
        var start = physicalKey.IndexOf('{') + 1;
        if (start <= 0 || start + 8 > physicalKey.Length)
            throw new InvalidOperationException("物理键缺少有效的分片哈希标签。");
        var hash = uint.Parse(physicalKey.Substring(start, 8), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
        return (int)(hash % 2);
    }

    /// <summary>
    /// 统计指定客户端上的已有缓存项。
    /// </summary>
    /// <returns>异步操作，完成后返回已存在的缓存项数量。</returns>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="keys">逻辑缓存键集合。</param>
    private static async Task<int> CountExistingAsync(IDatabase database, IEnumerable<string> keys)
    {
        var count = 0;
        foreach (var key in keys)
            if (await database.KeyExistsAsync(key))
                count++;
        return count;
    }

    /// <summary>
    /// 将缓存流读取为列表。
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
    /// 获取第一个 Redis 分片的连接字符串。
    /// </summary>
    private static string FirstConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");

    /// <summary>
    /// 获取第二个 Redis 分片的连接字符串。
    /// </summary>
    private static string SecondConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
}
