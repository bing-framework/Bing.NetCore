using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Globalization;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证客户端分片 Hybrid 缓存行为。
/// </summary>
/// <remarks>
/// 覆盖批量路径、跨实例失效和命名空间隔离。
/// </remarks>
public sealed class EasyShardedHybridIntegrationTest
{
    /// <summary>
    /// 客户端分片 Hybrid 跨实例完成批量读写、流式读取、失效传播和清理。
    /// </summary>
    [EasyShardedRedisFact]
    public async Task ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge()
    {
        var configuration = CreateConfiguration();
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName, topology: CacheTopology.ClientSharded, expectedShardCount: 2);
        using var firstContainer = BuildHybrid(configuration);
        using var secondContainer = BuildHybrid(configuration);
        var firstDiagnostics = firstContainer
            .GetServices<IHybridInvalidationDeliveryDiagnostics>()
            .ToArray();
        Assert.Equal(2, firstDiagnostics.Length);
        Assert.Equal(2, firstDiagnostics.Select(item => item.GetSnapshot().Topic)
            .Distinct(StringComparer.Ordinal).Count());
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstFactory = firstContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var secondFactory = secondContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var firstDistributed = GetProviders(firstFactory, configuration, "distributed");
        var secondDistributed = GetProviders(secondFactory, configuration, "distributed");
        var firstLocal = GetProviders(firstFactory, configuration, "local");
        var secondLocal = GetProviders(secondFactory, configuration, "local");
        var databases = firstDistributed.Select(provider =>
                Assert.IsAssignableFrom<IDatabase>(provider.Database))
            .ToArray();

        var prefix = "sharded-hybrid:" + Guid.NewGuid().ToString("N") + ":";
        var keys = CreateCrossShardKeys(configuration, prefix);
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var requested = new[] { keys[2], keys[0], prefix + "missing", keys[2], keys[1] };
        var initial = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, 100 + index))
            .ToDictionary(item => item.Key, item => item.Value);
        var updated = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, 1000 + index))
            .ToDictionary(item => item.Key, item => item.Value);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");
        var sentinelDatabase = databases[GetExpectedShard(sentinel)];
        var backfilledKey = prefix + "backfilled";
        var backfilledPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, backfilledKey);

        try
        {
            await sentinelDatabase.StringSetAsync(sentinel, "outside");
            await first.SetAllAsync(initial);

            AssertLayerValues(firstDistributed, configuration, physicalKeys,
                initial.Values.Select(value => (int?)value));
            AssertLayerValues(firstLocal, configuration, physicalKeys,
                initial.Values.Select(value => (int?)value));

            var list = await first.GetAllAsync<int?>(requested);
            var streamed = await ReadStreamAsync(first.GetAllStreamAsync<int?>(requested));
            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(new int?[]
            {
                initial[keys[2]], initial[keys[0]], null, initial[keys[2]], initial[keys[1]]
            }, list);
            Assert.Equal(list, streamed.Select(entry => entry.Value));
            Assert.Equal(new[] { true, true, false, true, true },
                streamed.Select(entry => entry.Hit));

            var prefixList = await first.GetByPrefixAsync<int>(prefix);
            var prefixStream = await ReadStreamAsync(first.GetByPrefixStreamAsync<int>(prefix));
            Assert.Equal(initial.Values.OrderBy(value => value), prefixList.OrderBy(value => value));
            Assert.Equal(prefixList.OrderBy(value => value),
                prefixStream.Select(entry => entry.Value).OrderBy(value => value));
            Assert.All(prefixStream, entry => Assert.True(entry.Hit));

            var secondInitial = await second.GetAllAsync<int>(keys);
            Assert.Equal(initial.Values, secondInitial);
            AssertLayerValues(secondLocal, configuration, physicalKeys,
                initial.Values.Select(value => (int?)value));

            await second.SetAllAsync(updated);
            AssertLayerValues(firstDistributed, configuration, physicalKeys,
                updated.Values.Select(value => (int?)value));
            AssertLayerValues(secondDistributed, configuration, physicalKeys,
                updated.Values.Select(value => (int?)value));
            await WaitForConditionAsync(
                () => physicalKeys.All(key => !GetProviderForKey(firstLocal, configuration, key)
                    .Get<int>(key).HasValue),
                "第二实例批量覆盖后，第一实例本地层未收到失效通知。");

            var converged = await first.GetAllAsync<int>(keys);
            Assert.Equal(updated.Values, converged);
            AssertLayerValues(firstLocal, configuration, physicalKeys,
                updated.Values.Select(value => (int?)value));
            AssertLayerValues(secondLocal, configuration, physicalKeys,
                updated.Values.Select(value => (int?)value));

            await first.RemoveAllAsync(keys);
            AssertLayerValues(firstDistributed, configuration, physicalKeys,
                new int?[keys.Length]);
            AssertLayerValues(secondDistributed, configuration, physicalKeys,
                new int?[keys.Length]);
            await WaitForConditionAsync(
                () => physicalKeys.All(key =>
                {
                    var firstValue = GetProviderForKey(firstLocal, configuration, key).Get<int>(key);
                    var secondValue = GetProviderForKey(secondLocal, configuration, key).Get<int>(key);
                    return !firstValue.HasValue && !secondValue.HasValue;
                }),
                "批量删除后两个实例的本地层未完成失效。");
            Assert.Equal(new int?[keys.Length], await second.GetAllAsync<int?>(keys));

            await first.SetAllAsync(updated);
            await second.GetAllAsync<int>(keys);
            AssertLayerValues(secondLocal, configuration, physicalKeys,
                updated.Values.Select(value => (int?)value));
            await first.ClearAsync();
            Assert.Empty(await second.GetByPrefixAsync<int>(prefix));
            Assert.All(physicalKeys, key =>
            {
                Assert.False(GetProviderForKey(firstLocal, configuration, key).Get<int>(key).HasValue);
                Assert.False(GetProviderForKey(secondLocal, configuration, key).Get<int>(key).HasValue);
            });
            Assert.True(await sentinelDatabase.KeyExistsAsync(sentinel));

            var calls = 0;
            var backfilled = await second.GetAsync(backfilledKey, _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(404);
            });
            Assert.Equal(404, backfilled);
            Assert.Equal(1, calls);
            Assert.Equal(404, GetProviderForKey(secondDistributed, configuration, backfilledPhysicalKey)
                .Get<int>(backfilledPhysicalKey).Value);
            Assert.Equal(404, GetProviderForKey(secondLocal, configuration, backfilledPhysicalKey)
                .Get<int>(backfilledPhysicalKey).Value);
            Assert.Equal(404, await second.GetAsync<int>(backfilledKey));
        }
        finally
        {
            try { await first.ClearAsync(); }
            catch { }
            try { await second.ClearAsync(); }
            catch { }
            try { await sentinelDatabase.KeyDeleteAsync(sentinel); }
            catch { }
        }
    }

    /// <summary>
    /// 创建客户端分片 Hybrid 缓存配置。
    /// </summary>
    /// <returns>返回配置了两个分片和批量参数的缓存配置。</returns>
    private static CacheConfiguration CreateConfiguration() =>
        new("easy-sharded-hybrid-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-hybrid-" + Guid.NewGuid().ToString("N"),
            batchSize: 2, maxConcurrency: 2, topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);

    /// <summary>
    /// 创建配置了两个 Redis 分片和 Hybrid 总线的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册客户端分片 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedHybridCaching(configuration,
            new[] { ConfigureNode(FirstConnection), ConfigureNode(SecondConnection) },
            bus => bus.ConfigurationOptions = CreateBusOptions(FirstConnection));
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
    /// 创建连接指定端点的总线 Redis 配置选项。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回测试总线使用的 Redis 连接选项。</returns>
    private static ConfigurationOptions CreateBusOptions(string connection)
    {
        var endpoint = ParseEndpoint(connection);
        var options = new ConfigurationOptions { AbortOnConnectFail = false };
        options.EndPoints.Add(endpoint.Host, endpoint.Port);
        return options;
    }

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
    /// 获取指定配置和缓存层级对应的全部分片 Provider。
    /// </summary>
    /// <param name="factory">缓存 Provider 工厂。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="tier">缓存层级。</param>
    /// <returns>返回按分片索引排列的 Provider 数组。</returns>
    private static IEasyCachingProvider[] GetProviders(IEasyCachingProviderFactory factory,
        CacheConfiguration configuration, string tier)
    {
        return Enumerable.Range(0, configuration.ExpectedShardCount!.Value)
            .Select(index => factory.GetCachingProvider(
                configuration.ProviderName + ":shard:" + index + ":" + tier))
            .ToArray();
    }

    /// <summary>
    /// 根据缓存键获取路由 Provider。
    /// </summary>
    /// <param name="providers">Provider 集合。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="physicalKey">物理缓存键。</param>
/// <returns>返回负责指定物理键的分片 Provider。</returns>
    private static IEasyCachingProvider GetProviderForKey(IReadOnlyList<IEasyCachingProvider> providers,
        CacheConfiguration configuration, string physicalKey) => providers[GetExpectedShard(physicalKey)];

    /// <summary>
    /// 断言指定缓存层中的物理键值与预期一致。
    /// </summary>
    /// <param name="providers">Provider 集合。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <param name="expected">预期值。</param>
    private static void AssertLayerValues(IReadOnlyList<IEasyCachingProvider> providers,
        CacheConfiguration configuration, IReadOnlyList<string> physicalKeys,
        IEnumerable<int?> expected)
    {
        var actual = physicalKeys.Select(key =>
        {
            var value = GetProviderForKey(providers, configuration, key).Get<int>(key);
            return value.HasValue ? (int?)value.Value : null;
        });
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// 生成覆盖两个客户端分片的测试键。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="prefix">缓存键前缀。</param>
    /// <returns>返回覆盖两个分片并保留测试顺序的逻辑键。</returns>
    private static string[] CreateCrossShardKeys(CacheConfiguration configuration, string prefix)
    {
        var keys = new string[4];
        for (var index = 0; index < 10000 && keys.Any(key => key == null); index++)
        {
            var key = prefix + index;
            var shard = GetExpectedShard(CacheKeyFormat.GetPhysicalKey(configuration, key));
            if (keys[shard] == null)
                keys[shard] = key;
            if (keys[2] == null && keys[0] != null && keys[1] != null)
                keys[2] = prefix + "extra-" + index;
            if (keys[3] == null && keys[2] != null)
                keys[3] = prefix + "tail-" + index;
        }

        if (keys.Any(key => key == null))
            throw new InvalidOperationException("无法生成覆盖两个客户端分片的测试键。");
        return keys;
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
    /// 等待测试条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="failureMessage">失败提示信息。</param>
    private static async Task WaitForConditionAsync(Func<bool> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(failureMessage);
            await Task.Delay(50);
        }
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
