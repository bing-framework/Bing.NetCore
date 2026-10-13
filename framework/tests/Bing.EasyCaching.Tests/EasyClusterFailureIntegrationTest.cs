using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Globalization;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching Redis Cluster 副本提升故障门控。
/// </summary>
public sealed class EasyClusterFailureFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyClusterFailureFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterFailureFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_FAULT_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要在一次性三主三从 Redis Cluster 上设置 RUN_INTEGRATION_TESTS=true 和 RUN_CACHE_CLUSTER_FAULT_TESTS=true。";
    }
}

/// <summary>
/// 验证 Redis Cluster 副本接管后的拓扑恢复。
/// </summary>
/// <remarks>
/// EasyCaching Redis 应重新发现完整拓扑，并恢复跨槽读取和清理。
/// </remarks>
public sealed class EasyClusterFailureIntegrationTest
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
    /// 副本接管后重建 CacheManager，并验证跨槽读取和清理仍覆盖全部键。
    /// </summary>
    [EasyClusterFailureFact]
    public async Task RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots()
    {
        var configuration = new CacheConfiguration(
            "easy-cluster-failover-" + Guid.NewGuid().ToString("N"),
            "easy-cluster-failover-" + Guid.NewGuid().ToString("N"),
            batchSize: 16, maxConcurrency: 4, topology: CacheTopology.RedisCluster);
        var prefix = "cluster-failover:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120)
            .Select(index => prefix + index)
            .ToArray();
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value);
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();

        Assert.True(physicalKeys.Select(CacheClusterTopology.GetHashSlot).Distinct().Count() > 1,
            "故障转移测试必须覆盖多个 Redis Cluster 槽位。");

        using var initial = BuildRedis(configuration);
        var initialCache = initial.GetRequiredService<IRedisCache>();
        var initialDatabase = GetDatabase(initial, configuration.ProviderName);
        using var control = await ConnectClusterAsync();

        try
        {
            await initialCache.SetAllAsync(values);
            Assert.Equal(values.Count, (await initialCache.GetByPrefixAsync<int>(prefix)).Count);

            var before = await WaitForStableTopologyAsync(control, _ => true);
            var replica = before.Replicas.First();
            var oldMasterId = replica.MasterId;

            using (var replicaControl = await ConnectNodeAsync(replica.Port))
            {
                var replicaServer = replicaControl.GetServer(Host, replica.Port);
                await replicaServer.ExecuteAsync("CLUSTER", "FAILOVER", "TAKEOVER");
            }

            var after = await WaitForStableTopologyAsync(control,
                topology => topology.Masters.Any(node => node.Id == replica.Id) &&
                            topology.Masters.All(node => node.Id != oldMasterId));
            Assert.Contains(after.Masters, node => node.Id == replica.Id);
            Assert.DoesNotContain(after.Masters, node => node.Id == oldMasterId);
            Assert.Equal(3, after.Masters.Count);
            Assert.Equal(3, after.Replicas.Count);
            Assert.True(after.SlotsCovered);

            using var rebuilt = BuildRedis(configuration);
            var cache = rebuilt.GetRequiredService<IRedisCache>();
            var requested = keys.Reverse().Take(80)
                .Concat(new[] { keys[0], prefix + "missing" })
                .ToArray();
            var expected = requested.Select(key => values.TryGetValue(key, out var value) ? value : 0);
            Assert.Equal(expected, await cache.GetAllAsync<int>(requested));

            var prefixValues = await cache.GetByPrefixAsync<int>(prefix);
            Assert.Equal(values.Count, prefixValues.Count);
            var streamed = new List<CacheEntry<int>>();
            await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                streamed.Add(entry);
            Assert.Equal(values.Count, streamed.Count);
            Assert.All(streamed, entry => Assert.True(entry.Hit));
            Assert.Equal(values.Values.OrderBy(value => value),
                streamed.Select(entry => entry.Value).OrderBy(value => value));

            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int>(prefix));
        }
        finally
        {
            foreach (var physicalKey in physicalKeys)
            {
                try
                {
                    await initialDatabase.KeyDeleteAsync(physicalKey);
                }
                catch (RedisConnectionException)
                {
                    // 故障转移期间旧连接可能短暂不可用；主断言优先。
                }
                catch (RedisTimeoutException)
                {
                    // 故障转移期间旧连接可能短暂超时；主断言优先。
                }
            }
        }
    }

    /// <summary>
    /// 创建绑定指定配置的 Redis Cluster 服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildRedis(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
        {
            foreach (var port in ClusterPorts)
                options.DBConfig.Endpoints.Add(new ServerEndPoint(Host, port));
        });
        return services.BuildServiceProvider();
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
    /// 连接测试 Redis Cluster 的任一节点。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis Cluster 多路复用器。</returns>
    private static async Task<ConnectionMultiplexer> ConnectClusterAsync()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AllowAdmin = true
        };
        foreach (var port in ClusterPorts)
            options.EndPoints.Add(Host, port);
        return await ConnectionMultiplexer.ConnectAsync(options);
    }

    /// <summary>
    /// 连接测试 Redis Cluster 中指定端口的节点。
    /// </summary>
    /// <returns>异步操作，完成后返回指定节点的 Redis 多路复用器。</returns>
    /// <param name="port">Redis 节点客户端端口。</param>
    private static async Task<ConnectionMultiplexer> ConnectNodeAsync(int port)
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AllowAdmin = true
        };
        options.EndPoints.Add(Host, port);
        return await ConnectionMultiplexer.ConnectAsync(options);
    }

    /// <summary>
    /// 等待 Redis Cluster 达到完整且稳定的拓扑状态。
    /// </summary>
    /// <returns>异步操作，完成后返回稳定的 Redis Cluster 拓扑快照。</returns>
    /// <param name="multiplexer">Redis 多路复用器。</param>
    /// <param name="predicate">筛选条件。</param>
    private static async Task<ClusterTopology> WaitForStableTopologyAsync(
        ConnectionMultiplexer multiplexer, Func<ClusterTopology, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        Exception lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var topology = await ReadTopologyAsync(multiplexer);
                if (topology.IsStable && predicate(topology))
                    return topology;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            await Task.Delay(250);
        }

        throw new Xunit.Sdk.XunitException(
            "等待 Redis Cluster 拓扑稳定或副本提升超时。" +
            (lastError == null ? string.Empty : " 最近错误：" + lastError.Message));
    }

    /// <summary>
    /// 读取并解析 Redis Cluster 节点及槽位信息。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis Cluster 拓扑快照。</returns>
    /// <param name="multiplexer">Redis 多路复用器。</param>
    private static async Task<ClusterTopology> ReadTopologyAsync(ConnectionMultiplexer multiplexer)
    {
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            try
            {
                var server = multiplexer.GetServer(endpoint);
                if (!server.IsConnected)
                    continue;
                var nodes = (await server.ExecuteAsync("CLUSTER", "NODES")).ToString();
                var topology = ParseTopology(nodes);
                if (topology != null)
                    return topology;
            }
            catch (RedisConnectionException)
            {
                // 拓扑切换期间尝试下一个已连接节点。
            }
            catch (RedisTimeoutException)
            {
                // 拓扑切换期间尝试下一个已连接节点。
            }
        }

        throw new InvalidOperationException("无法从 Redis Cluster 读取节点清单。");
    }

    /// <summary>
    /// 将 CLUSTER NODES 文本解析为拓扑快照。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <returns>返回解析后的 Redis Cluster 拓扑快照。</returns>
    private static ClusterTopology ParseTopology(string nodes)
    {
        if (string.IsNullOrWhiteSpace(nodes))
            return null;

        var masters = new List<ClusterNode>();
        var replicas = new List<ClusterNode>();
        var covered = new bool[16384];
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                continue;
            var flags = fields[2].Split(',');
            var endpoint = fields[1].Split('@')[0];
            var port = ParsePort(endpoint);
            var connected = string.Equals(fields[7], "connected", StringComparison.Ordinal);
            var failed = flags.Contains("fail", StringComparer.Ordinal) ||
                         flags.Contains("fail?", StringComparer.Ordinal);
            if (port <= 0 || !connected || failed)
                continue;

            if (flags.Contains("master", StringComparer.Ordinal))
            {
                for (var index = 8; index < fields.Length; index++)
                {
                    if (!TryMarkSlotRange(fields[index], covered))
                        return null;
                }

                masters.Add(new ClusterNode(fields[0], port, null));
            }
            else if (flags.Contains("slave", StringComparer.Ordinal) ||
                     flags.Contains("replica", StringComparer.Ordinal))
            {
                replicas.Add(new ClusterNode(fields[0], port, fields[3]));
            }
        }

        return new ClusterTopology(masters, replicas, covered.All(value => value));
    }

    /// <summary>
    /// 标记槽位范围，并返回范围是否未与已有槽位重叠。
    /// </summary>
    /// <param name="value">待处理值。</param>
    /// <param name="covered">槽位覆盖标记。</param>
    /// <returns>如果槽位范围有效且未与已覆盖槽位重叠，则返回 true。</returns>
    private static bool TryMarkSlotRange(string value, bool[] covered)
    {
        if (value.StartsWith("[", StringComparison.Ordinal))
            return true;
        var range = value.Split('-');
        if (range.Length > 2 || !int.TryParse(range[0], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var start))
            return false;
        var end = start;
        if (range.Length == 2 && !int.TryParse(range[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out end))
            return false;
        if (start < 0 || end >= covered.Length || start > end)
            return false;
        for (var slot = start; slot <= end; slot++)
        {
            if (covered[slot])
                return false;
            covered[slot] = true;
        }

        return true;
    }

    /// <summary>
    /// 从 Redis 端点文本解析客户端端口。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回 Redis 客户端端口。</returns>
    private static int ParsePort(string endpoint)
    {
        var separator = endpoint.LastIndexOf(':');
        return separator < 0 || !int.TryParse(endpoint[(separator + 1)..],
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            ? -1
            : port;
    }

    /// <summary>
    /// 保存 Redis Cluster 拓扑快照。
    /// </summary>
    private sealed class ClusterTopology
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterTopology"/> 类型的实例。
        /// </summary>
        /// <param name="masters">主节点集合。</param>
        /// <param name="replicas">副本节点集合。</param>
        /// <param name="slotsCovered">槽位是否完整覆盖。</param>
        internal ClusterTopology(IReadOnlyList<ClusterNode> masters,
            IReadOnlyList<ClusterNode> replicas, bool slotsCovered)
        {
            Masters = masters;
            Replicas = replicas;
            SlotsCovered = slotsCovered;
        }

        /// <summary>
        /// 获取主节点集合。
        /// </summary>
        internal IReadOnlyList<ClusterNode> Masters { get; }

        /// <summary>
        /// 获取副本节点集合。
        /// </summary>
        internal IReadOnlyList<ClusterNode> Replicas { get; }

        /// <summary>
        /// 获取槽位是否完整覆盖。
        /// </summary>
        internal bool SlotsCovered { get; }

        /// <summary>
        /// 获取拓扑是否稳定。
        /// </summary>
        internal bool IsStable => SlotsCovered && Masters.Count == 3 && Replicas.Count == 3 &&
            Masters.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == 3 &&
            Replicas.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == 3 &&
            Masters.Select(node => node.Port).Distinct().Count() == 3 &&
            Replicas.Select(node => node.Port).Distinct().Count() == 3 &&
            Replicas.All(replica => Masters.Any(master => master.Id == replica.MasterId)) &&
            Masters.All(master => Replicas.Count(replica => replica.MasterId == master.Id) == 1);
    }

    /// <summary>
    /// 保存 Redis 节点测试信息。
    /// </summary>
    private sealed class ClusterNode
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterNode"/> 类型的实例。
        /// </summary>
        /// <param name="id">Redis Cluster 节点标识。</param>
        /// <param name="port">Redis 节点客户端端口。</param>
        /// <param name="masterId">所属主节点标识；主节点自身为空。</param>
        internal ClusterNode(string id, int port, string masterId)
        {
            Id = id;
            Port = port;
            MasterId = masterId;
        }

        /// <summary>
        /// 获取节点标识。
        /// </summary>
        internal string Id { get; }

        /// <summary>
        /// 获取 Redis 服务端口。
        /// </summary>
        internal int Port { get; }

        /// <summary>
        /// 获取所属主节点标识。
        /// </summary>
        internal string MasterId { get; }
    }
}
