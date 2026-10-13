using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 仅在隔离 Redis Cluster 故障测试开关启用时执行节点故障用例。
/// </summary>
public sealed class ClusterFailureFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterFailureFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterFailureFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_FAULT_TESTS") != "true")
            Skip = "需在隔离 Cluster 停止 7000 主节点后启用故障测试。";
    }
}

/// <summary>
/// 仅在集成测试和 Cluster 拓扑切换开关启用时执行主从切换用例。
/// </summary>
public sealed class ClusterTransitionFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterTransitionFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterTransitionFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_TRANSITION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需在可丢弃的三主三从 Redis Cluster 上同时启用 RUN_INTEGRATION_TESTS=true 和 RUN_CACHE_CLUSTER_TRANSITION_TESTS=true。";
    }
}

/// <summary>
/// 验证 Redis Cluster 主节点故障后的缓存访问行为。
/// </summary>
public class ClusterFailureTest
{
    /// <summary>
    /// 保存故障后仍可访问的 Redis 节点端点。
    /// </summary>
    private const string SurvivingSeedEndpoint = "127.0.0.1:7001";
    /// <summary>
    /// 保存故障主节点端口。
    /// </summary>
    private const int FailedMasterPort = 7000;
    /// <summary>
    /// 保存 Redis Cluster 槽位总数。
    /// </summary>
    private const int ClusterSlotCount = 16384;
    /// <summary>
    /// 保存预期主节点数量。
    /// </summary>
    private const int ExpectedMasterCount = 3;
    /// <summary>
    /// 保存预期副本节点数量。
    /// </summary>
    private const int ExpectedReplicaCount = 3;

    /// <summary>
    /// 保存 Redis Cluster 主节点标识和端点。
    /// </summary>
    private sealed class ClusterMaster
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterMaster"/> 类型的实例。
        /// </summary>
    /// <param name="id">主节点标识。</param>
    /// <param name="endpoint">主节点端点。</param>
        public ClusterMaster(string id, string endpoint)
        {
            Id = id;
            Endpoint = endpoint;
        }

        /// <summary>
        /// 获取主节点标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取主节点端点。
        /// </summary>
        public string Endpoint { get; }
    }

    /// <summary>
    /// 保存 Redis Cluster 节点状态及其主节点标识。
    /// </summary>
    private sealed class ClusterNode
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterNode"/> 类型的实例。
        /// </summary>
    /// <param name="id">节点标识。</param>
    /// <param name="endpoint">节点端点。</param>
    /// <param name="masterId">所属主节点标识。</param>
    /// <param name="isMaster">是否为主节点。</param>
    /// <param name="isReplica">是否为副本节点。</param>
    /// <param name="isConnected">节点是否已连接。</param>
    /// <param name="isFailed">节点是否已标记故障。</param>
        public ClusterNode(string id, string endpoint, string masterId, bool isMaster, bool isReplica,
            bool isConnected, bool isFailed)
        {
            Id = id;
            Endpoint = endpoint;
            MasterId = masterId;
            IsMaster = isMaster;
            IsReplica = isReplica;
            IsConnected = isConnected;
            IsFailed = isFailed;
        }

        /// <summary>
        /// 获取节点标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取节点端点。
        /// </summary>
        public string Endpoint { get; }
        /// <summary>
        /// 获取所属主节点标识。
        /// </summary>
        public string MasterId { get; }
        /// <summary>
        /// 获取节点是否为主节点。
        /// </summary>
        public bool IsMaster { get; }
        /// <summary>
        /// 获取节点是否为副本节点。
        /// </summary>
        public bool IsReplica { get; }
        /// <summary>
        /// 获取节点是否已连接。
        /// </summary>
        public bool IsConnected { get; }
        /// <summary>
        /// 获取节点是否已标记故障。
        /// </summary>
        public bool IsFailed { get; }
    }

    /// <summary>
    /// 保存 Redis Cluster 节点列表和槽位覆盖状态。
    /// </summary>
    private sealed class ClusterTopologySnapshot
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterTopologySnapshot"/> 类型的实例。
        /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <param name="slotsAreCovered">槽位是否已完整覆盖。</param>
        public ClusterTopologySnapshot(IReadOnlyList<ClusterNode> nodes, bool slotsAreCovered)
        {
            Nodes = nodes;
            SlotsAreCovered = slotsAreCovered;
            Masters = nodes.Where(node => node.IsMaster).ToArray();
            Replicas = nodes.Where(node => node.IsReplica).ToArray();
        }

        /// <summary>
        /// 获取 Redis 节点集合。
        /// </summary>
        public IReadOnlyList<ClusterNode> Nodes { get; }
        /// <summary>
        /// 获取主节点集合。
        /// </summary>
        public IReadOnlyList<ClusterNode> Masters { get; }
        /// <summary>
        /// 获取副本节点集合。
        /// </summary>
        public IReadOnlyList<ClusterNode> Replicas { get; }
        /// <summary>
        /// 获取槽位是否已完整覆盖。
        /// </summary>
        public bool SlotsAreCovered { get; }
        /// <summary>
        /// 获取拓扑是否满足稳定条件。
        /// </summary>
        public bool IsStable =>
            SlotsAreCovered &&
            Masters.Count == ExpectedMasterCount &&
            Replicas.Count == ExpectedReplicaCount &&
            Masters.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == ExpectedMasterCount &&
            Replicas.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == ExpectedReplicaCount &&
            Masters.Select(node => node.Endpoint).Distinct(StringComparer.Ordinal).Count() == ExpectedMasterCount &&
            Replicas.Select(node => node.Endpoint).Distinct(StringComparer.Ordinal).Count() == ExpectedReplicaCount &&
            Nodes.All(node => node.IsConnected && !node.IsFailed) &&
            Replicas.All(node => Masters.Any(master => master.Id == node.MasterId));
    }

    /// <summary>
    /// 缺少在线主节点时拒绝扫描和清理，且已有缓存数据仍可读取。
    /// </summary>
    [ClusterFailureFact]
    public async Task OmittedOnlineMaster_RejectsScanAndClearBeforePartialSuccess()
    {
        var connections = new[] { "127.0.0.1:7001", "127.0.0.1:7002" };
        using var router = new RedisClient(connections.Select(value => (ConnectionStringBuilder)value).ToArray());
        router.Serialize = JsonConvert.SerializeObject;
        router.Deserialize = JsonConvert.DeserializeObject;
        var masters = connections.Select(value => new RedisClient(value)).ToArray();
        try
        {
            var configuration = new CacheConfiguration("free-cluster-omitted-" + Guid.NewGuid().ToString("N"),
                "redis", topology: CacheTopology.RedisCluster);
            var cache = new FreeRedisCacheManager(router, configuration, masters);
            for (var index = 0; index < 120; index++)
                await cache.SetAsync("item:" + index, index + 1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetByPrefixAsync<int>("item:"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ClearAsync());
            Assert.Equal(Enumerable.Range(1, 120),
                await cache.GetAllAsync<int>(Enumerable.Range(0, 120).Select(index => "item:" + index)));
        }
        finally
        {
            foreach (var master in masters) master.Dispose();
        }
    }

    /// <summary>
    /// 主从切换后根据新主节点拓扑读取全部槽位并清理缓存。
    /// </summary>
    [ClusterFailureFact]
    public async Task PromotedReplica_UpdatedTopologyReadsAndClearsAllSlots()
    {
        var masters = await WaitForPromotedMastersAsync();
        var connections = masters.Select(master => master.Endpoint).ToArray();
        using var router = new RedisClient(connections.Select(value => (ConnectionStringBuilder)value).ToArray());
        router.Serialize = JsonConvert.SerializeObject;
        router.Deserialize = JsonConvert.DeserializeObject;
        var scanClients = connections.Select(value => new RedisClient(value)).ToArray();
        try
        {
            foreach (var master in scanClients)
            {
                master.Serialize = JsonConvert.SerializeObject;
                master.Deserialize = JsonConvert.DeserializeObject;
            }
            var cache = new FreeRedisCacheManager(router,
                new CacheConfiguration("free-cluster-promoted-" + Guid.NewGuid().ToString("N"),
                    "redis", topology: CacheTopology.RedisCluster), scanClients);
            for (var index = 0; index < 120; index++)
                await cache.SetAsync("item:" + index, index + 1);

            Assert.Equal(Enumerable.Range(1, 120),
                await cache.GetAllAsync<int>(Enumerable.Range(0, 120).Select(index => "item:" + index)));
            Assert.Equal(120, (await cache.GetByPrefixAsync<int>("item:")).Count);
            await cache.ClearAsync();
            Assert.Empty(await cache.GetByPrefixAsync<int>("item:"));
        }
        finally
        {
            foreach (var master in scanClients) master.Dispose();
        }
    }

    /// <summary>
    /// 缺少扫描主节点时抛出错误而不返回局部结果。
    /// </summary>
    [ClusterFailureFact]
    public async Task MissingScanMaster_FailsInsteadOfReturningPartialResults()
    {
        var connections = new[] { "127.0.0.1:7000", "127.0.0.1:7001", "127.0.0.1:7002" };
        using var router = new RedisClient(connections.Select(value => (ConnectionStringBuilder)value).ToArray());
        router.Serialize = JsonConvert.SerializeObject;
        router.Deserialize = JsonConvert.DeserializeObject;
        var masters = connections.Select(value => new RedisClient(value)).ToArray();
        try
        {
            var cache = new FreeRedisCacheManager(router,
                new CacheConfiguration("free-cluster-fault-" + Guid.NewGuid().ToString("N"),
                    "redis", topology: CacheTopology.RedisCluster), masters);

            await Assert.ThrowsAnyAsync<Exception>(() => cache.GetByPrefixAsync<int>("item:"));
            await Assert.ThrowsAnyAsync<Exception>(() => cache.ClearAsync());
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await foreach (var _ in cache.GetByPrefixStreamAsync<int>("item:"))
                {
                }
            });
        }
        finally
        {
            foreach (var master in masters) master.Dispose();
        }
    }

    /// <summary>
    /// 副本接管后拒绝使用过期主节点视图扫描或清理。
    /// </summary>
    [ClusterTransitionFact]
    public async Task StaleScanClients_RejectScanAndClearAfterReplicaTakeover()
    {
        var seedEndpoint = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(seedEndpoint))
            throw new InvalidOperationException("Redis Cluster 拓扑切换测试要求设置 ConnectionStrings__RedisConnection。");

        using var discovery = CreateClient(seedEndpoint);
        var initial = await WaitForStableTopologyAsync(discovery, null, null);
        Assert.Equal(ExpectedMasterCount, initial.Masters.Count);
        Assert.Equal(ExpectedReplicaCount, initial.Replicas.Count);

        var replica = initial.Replicas.FirstOrDefault(candidate =>
            initial.Masters.Any(master => master.Id == candidate.MasterId));
        Assert.NotNull(replica);

        var initialMasterIds = initial.Masters.Select(master => master.Id)
            .ToHashSet(StringComparer.Ordinal);
        var masterEndpoints = initial.Masters.Select(master => master.Endpoint).ToArray();
        using var router = CreateClusterRouter(masterEndpoints);
        var scanClients = masterEndpoints.Select(CreateClient).ToArray();
        using var takeoverClient = CreateClient(replica.Endpoint);
        try
        {
            var configuration = new CacheConfiguration(
                "free-cluster-transition-" + Guid.NewGuid().ToString("N"),
                "redis", topology: CacheTopology.RedisCluster, maxConcurrency: 4);
            var cache = new FreeRedisCacheManager(router, configuration, scanClients);
            var values = Enumerable.Range(0, 120)
                .ToDictionary(index => "transition-item:" + index, index => index + 1);

            await cache.SetAllAsync(values);
            Assert.Equal(values.Count, (await cache.GetByPrefixAsync<int>("transition-item:")).Count);

            await takeoverClient.CallAsync(
                new CommandPacket("CLUSTER", "FAILOVER").Input(new object[] { "TAKEOVER" }));
            var transitioned = await WaitForStableTopologyAsync(discovery, initialMasterIds, replica.Id);

            Assert.True(transitioned.IsStable, "Redis Cluster 拓扑切换后必须稳定覆盖全部 16384 个槽位。");
            Assert.Contains(replica.Id, transitioned.Masters.Select(master => master.Id));
            Assert.False(initialMasterIds.SetEquals(transitioned.Masters.Select(master => master.Id)));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cache.GetByPrefixAsync<int>("transition-item:"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ClearAsync());
        }
        finally
        {
            foreach (var scanClient in scanClients)
                scanClient.Dispose();
        }
    }

    /// <summary>
    /// 等待故障后的 Redis Cluster 主节点列表稳定。
    /// </summary>
    /// <returns>包含已提升主节点的异步任务。</returns>
    private static async Task<IReadOnlyList<ClusterMaster>> WaitForPromotedMastersAsync()
    {
        using var discovery = new RedisClient(SurvivingSeedEndpoint);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var nodes = await discovery.CallAsync(new CommandPacket("CLUSTER", "NODES"));
                if (TryReadCurrentMasters(nodes as string, out var masters))
                    return masters;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new Xunit.Sdk.XunitException(
            "等待 Redis Cluster 在 7000 停止后完成三主节点、16384 槽位覆盖和副本晋升超时。" +
            (lastError == null ? string.Empty : " 最近错误: " + lastError.Message));
    }

    /// <summary>
    /// 等待 Redis Cluster 拓扑满足指定条件。
    /// </summary>
    /// <returns>异步操作，完成后返回稳定的 Redis Cluster 拓扑快照。</returns>
    /// <param name="discovery">拓扑发现结果。</param>
    /// <param name="previousMasterIds">故障前的主节点标识集合。</param>
    /// <param name="promotedReplicaId">对象标识。</param>
    private static async Task<ClusterTopologySnapshot> WaitForStableTopologyAsync(
        RedisClient discovery, IReadOnlySet<string> previousMasterIds, string promotedReplicaId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        Exception lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var snapshot = await ReadTopologyAsync(discovery);
                var masterIds = snapshot.Masters.Select(master => master.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (snapshot.IsStable &&
                    (previousMasterIds == null || !previousMasterIds.SetEquals(masterIds)) &&
                    (promotedReplicaId == null || masterIds.Contains(promotedReplicaId)))
                    return snapshot;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new Xunit.Sdk.XunitException(
            "等待 Redis Cluster 拓扑稳定并覆盖全部 16384 个槽位超时。" +
            (lastError == null ? string.Empty : " 最近错误: " + lastError.Message));
    }

    /// <summary>
    /// 读取并解析 Redis Cluster 节点清单。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis Cluster 拓扑快照。</returns>
    /// <param name="discovery">拓扑发现结果。</param>
    private static async Task<ClusterTopologySnapshot> ReadTopologyAsync(RedisClient discovery)
    {
        var response = await discovery.CallAsync(new CommandPacket("CLUSTER", "NODES"));
        if (!(response is string nodes) || string.IsNullOrWhiteSpace(nodes))
            throw new InvalidOperationException("无法读取 Redis Cluster 节点清单。");
        return ParseTopology(nodes);
    }

    /// <summary>
    /// 解析 Redis Cluster 节点清单及各槽位的当前归属。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <returns>解析出的节点和槽位归属快照。</returns>
    private static ClusterTopologySnapshot ParseTopology(string nodes)
    {
        var parsed = new List<ClusterNode>();
        var coveredSlots = new bool[ClusterSlotCount];
        var slotsAreCovered = true;
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                throw new InvalidOperationException("Redis Cluster 节点清单格式无效。");

            var flags = fields[2].Split(',');
            var isMaster = flags.Contains("master", StringComparer.Ordinal);
            var isReplica = flags.Contains("slave", StringComparer.Ordinal) ||
                flags.Contains("replica", StringComparer.Ordinal);
            var isFailed = flags.Contains("fail", StringComparer.Ordinal) ||
                flags.Contains("fail?", StringComparer.Ordinal);
            var isConnected = string.Equals(fields[7], "connected", StringComparison.Ordinal);
            var endpoint = fields[1].Split('@')[0];
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("Redis Cluster 节点端点为空。");

            if (isMaster)
            {
                for (var index = 8; index < fields.Length; index++)
                {
                    if (fields[index].StartsWith("[", StringComparison.Ordinal))
                    {
                        slotsAreCovered = false;
                        continue;
                    }
                    if (!TryMarkSlotRange(fields[index], coveredSlots))
                        slotsAreCovered = false;
                }
            }

            parsed.Add(new ClusterNode(fields[0], endpoint, fields[3], isMaster, isReplica,
                isConnected, isFailed));
        }

        if (coveredSlots.Any(slot => !slot))
            slotsAreCovered = false;
        return new ClusterTopologySnapshot(parsed, slotsAreCovered);
    }

    /// <summary>
    /// 标记 Redis Cluster 槽位范围并检测重复覆盖。
    /// </summary>
    /// <param name="value">待处理值。</param>
    /// <param name="coveredSlots">已覆盖槽位的标记数组。</param>
    /// <returns>如果范围有效且未重复覆盖，则返回 <see langword="true" />。</returns>
    private static bool TryMarkSlotRange(string value, bool[] coveredSlots)
    {
        var range = value.Split('-');
        if (range.Length > 2 || !int.TryParse(range[0], out var start))
            return false;
        var end = start;
        if (range.Length == 2 && !int.TryParse(range[1], out end))
            return false;
        if (start < 0 || end >= coveredSlots.Length || start > end)
            return false;
        for (var slot = start; slot <= end; slot++)
        {
            if (coveredSlots[slot])
                return false;
            coveredSlots[slot] = true;
        }
        return true;
    }

    /// <summary>
    /// 创建使用 JSON 序列化的 Redis 测试客户端。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>配置了 JSON 序列化的 Redis 客户端。</returns>
    private static RedisClient CreateClient(string endpoint)
    {
        var client = new RedisClient(endpoint)
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        return client;
    }

    /// <summary>
    /// 创建用于跨节点路由测试的 Redis Cluster 客户端。
    /// </summary>
    /// <param name="endpoints">Redis 端点集合。</param>
    /// <returns>配置了种子节点的 Redis Cluster 路由客户端。</returns>
    private static RedisClient CreateClusterRouter(IReadOnlyList<string> endpoints)
    {
        var router = new RedisClient(endpoints.Select(endpoint => (ConnectionStringBuilder)endpoint).ToArray())
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        return router;
    }

    /// <summary>
    /// 解析当前仍可用的 Redis Cluster 主节点。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <param name="masters">主节点集合。</param>
    /// <returns>如果节点清单包含完整稳定的主节点集合，则返回 <see langword="true" />。</returns>
    private static bool TryReadCurrentMasters(string nodes, out IReadOnlyList<ClusterMaster> masters)
    {
        masters = null;
        if (string.IsNullOrWhiteSpace(nodes))
            return false;

        var current = new List<ClusterMaster>();
        var covered = new bool[16384];
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                continue;
            var flags = fields[2].Split(',');
            if (!flags.Contains("master") || flags.Contains("fail") || flags.Contains("fail?") ||
                !string.Equals(fields[7], "connected", StringComparison.Ordinal))
                continue;

            var endpoint = fields[1].Split('@')[0];
            if (GetPort(endpoint) == FailedMasterPort)
                return false;
            foreach (var token in fields.Skip(8))
            {
                if (token.StartsWith("[", StringComparison.Ordinal))
                    continue;
                var range = token.Split('-');
                if (range.Length > 2 || !int.TryParse(range[0], out var start))
                    return false;
                var last = start;
                if (range.Length == 2 && !int.TryParse(range[1], out last))
                    return false;
                if (start < 0 || last >= covered.Length || start > last)
                    return false;
                for (var slot = start; slot <= last; slot++)
                {
                    if (covered[slot])
                        return false;
                    covered[slot] = true;
                }
            }
            current.Add(new ClusterMaster(fields[0], endpoint));
        }

        if (current.Count != 3 || current.Select(master => master.Id).Distinct(StringComparer.Ordinal).Count() != 3 ||
            current.Select(master => master.Endpoint).Distinct(StringComparer.Ordinal).Count() != 3 ||
            covered.Any(slot => !slot))
            return false;

        masters = current;
        return true;
    }

    /// <summary>
    /// 解析测试 Redis 端口。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回端点中的端口号，无法解析时返回 -1。</returns>
    private static int GetPort(string endpoint)
    {
        var separator = endpoint.LastIndexOf(':');
        return separator < 0 || !int.TryParse(endpoint.Substring(separator + 1), out var port) ? -1 : port;
    }
}
