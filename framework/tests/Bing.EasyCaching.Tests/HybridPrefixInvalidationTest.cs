using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// Hybrid 前缀失效测试门控。
/// </summary>
/// <remarks>
/// 仅在数据端点明确指向本地专用 52800 Redis 时运行。
/// </remarks>
public sealed class EasyHybridPrefix52800FactAttribute : EasyHybridBusFactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyHybridPrefix52800FactAttribute"/> 类型的实例。
    /// </summary>
    public EasyHybridPrefix52800FactAttribute()
    {
        if (!string.IsNullOrEmpty(Skip))
            return;

        var connection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        if (!TryGetPort(connection, out var port) || port != 52800)
            Skip = "需要将 BING_TEST_EASYCACHING_REDIS 指向专用的 52800 单节点 Redis。";
    }

    /// <summary>
    /// 从 Redis 连接字符串中读取端口号。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="port">解析出的 Redis 端口号。</param>
    /// <returns>返回连接字符串是否包含可解析的端口。</returns>
    private static bool TryGetPort(string connection, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(connection))
            return false;
        var endpoint = connection.Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        return separator > 0 &&
            int.TryParse(endpoint[(separator + 1)..], out port);
    }
}

/// <summary>
/// 验证单节点 Hybrid 前缀删除能够清理两个实例的本地层和共享分布式层。
/// </summary>
public sealed class HybridPrefixInvalidationTest
{
    /// <summary>
    /// 单节点 Hybrid 的同步和异步前缀删除清理两个实例的本地层与分布式层。
    /// </summary>
    [EasyHybridPrefix52800Fact]
    public async Task Hybrid_RemoveByPrefix_SyncAndAsyncClearLocalAndDistributedLayers()
    {
        var configuration = new CacheConfiguration(
            "hybrid-prefix-" + Guid.NewGuid().ToString("N"),
            "hybrid-prefix-" + Guid.NewGuid().ToString("N"),
            batchSize: 2, maxConcurrency: 2);
        var dataEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        var busEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        using var firstContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint);
        using var secondContainer = BuildHybrid(configuration, dataEndpoint, busEndpoint);
        using var busControl = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
            AllowAdmin = true
        });

        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstFactory = firstContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var secondFactory = secondContainer.GetRequiredService<IEasyCachingProviderFactory>();
        var firstDistributed = firstFactory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var secondDistributed = secondFactory.GetCachingProvider(configuration.ProviderName + ":distributed");
        var firstLocal = firstFactory.GetCachingProvider(configuration.ProviderName + ":local");
        var secondLocal = secondFactory.GetCachingProvider(configuration.ProviderName + ":local");
        var database = Assert.IsAssignableFrom<IDatabase>(firstDistributed.Database);
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var topic = configuration.ProviderName + ":invalidations";
        var prefix = "prefix:" + Guid.NewGuid().ToString("N") + ":";
        var keys = new[] { prefix + "one", prefix + "two", prefix + "three" };
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var otherConfiguration = new CacheConfiguration(configuration.Namespace + "-other",
            configuration.ProviderName);
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");

        try
        {
            await database.StringSetAsync(sentinel, "outside");
            await WaitForSubscribersAsync(busServer, topic, 2);

            await first.SetAllAsync(values);
            Assert.Equal(values.Values, await first.GetAllAsync<int>(keys));
            AssertLayerValues(firstDistributed, physicalKeys, values.Values);
            await WarmLocalLayersAsync(first, second, firstLocal, secondLocal, keys, physicalKeys);
            AssertLayerValues(firstLocal, physicalKeys, values.Values);

            Assert.Equal(values.Values, await second.GetAllAsync<int>(keys));
            AssertLayerValues(secondDistributed, physicalKeys, values.Values);
            AssertLayerValues(secondLocal, physicalKeys, values.Values);

            first.RemoveByPrefix(prefix);
            await WaitUntilAsync(() => LayersMissing(firstDistributed, secondDistributed,
                    firstLocal, secondLocal, physicalKeys),
                "同步 RemoveByPrefix 后本地层或分布式层仍有缓存项。");
            Assert.Empty(await first.GetByPrefixAsync<int>(prefix));
            Assert.Empty(await second.GetByPrefixAsync<int>(prefix));
            Assert.True(await database.KeyExistsAsync(sentinel));

            await first.SetAllAsync(values);
            Assert.Equal(values.Values, await first.GetAllAsync<int>(keys));
            Assert.Equal(values.Values, await second.GetAllAsync<int>(keys));
            await WarmLocalLayersAsync(first, second, firstLocal, secondLocal, keys, physicalKeys);
            AssertLayerValues(firstDistributed, physicalKeys, values.Values);
            AssertLayerValues(secondDistributed, physicalKeys, values.Values);
            AssertLayerValues(firstLocal, physicalKeys, values.Values);
            AssertLayerValues(secondLocal, physicalKeys, values.Values);

            await second.RemoveByPrefixAsync(prefix);
            await WaitUntilAsync(() => LayersMissing(firstDistributed, secondDistributed,
                    firstLocal, secondLocal, physicalKeys),
                "异步 RemoveByPrefix 后本地层或分布式层仍有缓存项。");
            Assert.Empty(await first.GetByPrefixAsync<int>(prefix));
            Assert.Empty(await second.GetByPrefixAsync<int>(prefix));
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            try { await first.RemoveByPrefixAsync(prefix); }
            catch { }
            await database.KeyDeleteAsync(sentinel);
        }
    }

    /// <summary>
    /// 将测试键写入两个实例的本地层和共享分布式层。
    /// </summary>
    /// <param name="first">第一个 Redis 缓存实例。</param>
    /// <param name="second">第二个 Redis 缓存实例。</param>
    /// <param name="firstLocal">第一个本地缓存。</param>
    /// <param name="secondLocal">第二个本地缓存。</param>
    /// <param name="keys">逻辑缓存键集合。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    private static Task WarmLocalLayersAsync(IRedisCache first, IRedisCache second,
        IEasyCachingProvider firstLocal, IEasyCachingProvider secondLocal,
        string[] keys, string[] physicalKeys) => WaitUntilAsync(() =>
        {
            first.GetAll<int>(keys);
            second.GetAll<int>(keys);
            return physicalKeys.All(key => firstLocal.Get<int>(key).HasValue && secondLocal.Get<int>(key).HasValue);
        }, "失效通知处理后无法回填两个本地层。");

    /// <summary>
    /// 创建配置了 Redis 分布式层、本地层和总线的 Hybrid 服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="dataEndpoint">Redis 端点。</param>
    /// <param name="busEndpoint">Redis 端点。</param>
    /// <returns>返回已注册 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration,
        (string Host, int Port) dataEndpoint, (string Host, int Port) busEndpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint(dataEndpoint.Host, dataEndpoint.Port)),
            bus => bus.ConfigurationOptions = new ConfigurationOptions
            {
                EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
                AbortOnConnectFail = false,
                SyncTimeout = 5000,
                AsyncTimeout = 5000
            });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 断言指定 Provider 中的物理键值与预期一致。
    /// </summary>
    /// <param name="provider">缓存 Provider。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <param name="expected">预期值。</param>
    private static void AssertLayerValues(IEasyCachingProvider provider,
        IReadOnlyList<string> physicalKeys, IEnumerable<int> expected)
    {
        Assert.Equal(expected, physicalKeys.Select(key =>
        {
            var value = provider.Get<int>(key);
            Assert.True(value.HasValue, "缓存层缺少预期的物理键。\n" + key);
            return value.Value;
        }));
    }

    /// <summary>
    /// 判断两个缓存层是否都缺少指定物理键。
    /// </summary>
    /// <param name="firstDistributed">第一个实例的分布式缓存 Provider。</param>
    /// <param name="secondDistributed">第二个实例的分布式缓存 Provider。</param>
    /// <param name="firstLocal">第一个本地缓存。</param>
    /// <param name="secondLocal">第二个本地缓存。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <returns>如果两个分布式层和本地层都缺少这些键，则返回 true。</returns>
    private static bool LayersMissing(IEasyCachingProvider firstDistributed,
        IEasyCachingProvider secondDistributed, IEasyCachingProvider firstLocal,
        IEasyCachingProvider secondLocal, IReadOnlyList<string> physicalKeys) =>
        physicalKeys.All(key =>
            !firstDistributed.Get<int>(key).HasValue &&
            !secondDistributed.Get<int>(key).HasValue &&
            !firstLocal.Get<int>(key).HasValue &&
            !secondLocal.Get<int>(key).HasValue);

    /// <summary>
    /// 等待 Redis 订阅者数量达到预期值。
    /// </summary>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="topic">发布主题。</param>
    /// <param name="expected">预期值。</param>
    private static async Task WaitForSubscribersAsync(IServer server, string topic, long expected)
    {
        await WaitUntilAsync(async () =>
        {
            var result = (RedisResult[])await server.ExecuteAsync("PUBSUB", "NUMSUB", topic);
            return (long)result[1] >= expected;
        }, $"Redis 总线主题 {topic} 未恢复 {expected} 个订阅。");
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<bool> condition, string error)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()), error);
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string error)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(error);
            await Task.Delay(100);
        }
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
}
