using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 控制 Redis Cluster 节点故障测试是否执行。
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
/// 控制 Redis Cluster 主从切换测试是否执行。
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
            Skip = "需在专用可丢弃 Redis Cluster 中同时启用 RUN_INTEGRATION_TESTS=true 和 RUN_CACHE_CLUSTER_TRANSITION_TESTS=true。";
    }
}

/// <summary>
/// 验证 Redis Cluster 节点故障及主从切换期间的缓存访问。
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
    /// 保存 Redis Cluster 副本节点及其主节点标识。
    /// </summary>
    private sealed class ClusterReplica
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterReplica"/> 类型的实例。
        /// </summary>
    /// <param name="id">副本节点标识。</param>
    /// <param name="endpoint">副本节点端点。</param>
    /// <param name="masterId">所属主节点标识。</param>
        public ClusterReplica(string id, string endpoint, string masterId)
        {
            Id = id;
            Endpoint = endpoint;
            MasterId = masterId;
        }

        /// <summary>
        /// 获取副本节点标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取副本节点端点。
        /// </summary>
        public string Endpoint { get; }
        /// <summary>
        /// 获取所属主节点标识。
        /// </summary>
        public string MasterId { get; }
    }

    /// <summary>
    /// 保存 Redis Cluster 主副本节点及拓扑签名。
    /// </summary>
    private sealed class ClusterTopologySnapshot
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterTopologySnapshot"/> 类型的实例。
        /// </summary>
    /// <param name="masters">主节点集合。</param>
    /// <param name="replicas">副本节点集合。</param>
    /// <param name="signature">拓扑签名。</param>
        public ClusterTopologySnapshot(IReadOnlyList<ClusterMaster> masters,
            IReadOnlyList<ClusterReplica> replicas, string signature)
        {
            Masters = masters;
            Replicas = replicas;
            Signature = signature;
        }

        /// <summary>
        /// 获取主节点集合。
        /// </summary>
        public IReadOnlyList<ClusterMaster> Masters { get; }
        /// <summary>
        /// 获取副本节点集合。
        /// </summary>
        public IReadOnlyList<ClusterReplica> Replicas { get; }
        /// <summary>
        /// 获取拓扑签名。
        /// </summary>
        public string Signature { get; }
    }

    /// <summary>
    /// 验证主节点提升后刷新集群拓扑仍可读取各槽位数据，并能清理全部键。
    /// </summary>
    [ClusterFailureFact]
    public async Task PromotedReplica_UpdatedTopologyReadsAndClearsAllSlots()
    {
        var masters = await WaitForPromotedMastersAsync();
        var clients = masters.Select(master => new CSRedisClient(master.Endpoint)).ToArray();
        using var router = clients[0];
        try
        {
            var cache = new CSRedisCacheManager(router,
                new CacheConfiguration("cs-cluster-promoted-" + Guid.NewGuid().ToString("N"),
                    "redis", topology: CacheTopology.RedisCluster), clients);
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
            foreach (var client in clients.Skip(1)) client.Dispose();
        }
    }

    /// <summary>
    /// 验证扫描主节点缺失时返回失败，而不是返回部分结果。
    /// </summary>
    [ClusterFailureFact]
    public async Task MissingScanMaster_FailsInsteadOfReturningPartialResults()
    {
        using var router = new CSRedisClient("127.0.0.1:7001");
        using var missing = new CSRedisClient("127.0.0.1:7000");
        var cache = new CSRedisCacheManager(router,
            new CacheConfiguration("cs-cluster-fault-" + Guid.NewGuid().ToString("N"),
                "redis", topology: CacheTopology.RedisCluster), new[] { router, missing });

        await Assert.ThrowsAnyAsync<Exception>(() => cache.GetByPrefixAsync<int>("item:"));
        await Assert.ThrowsAnyAsync<Exception>(() => cache.ClearAsync());
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in cache.GetByPrefixStreamAsync<int>("item:"))
            {
            }
        });
    }

    /// <summary>
    /// 验证缓存创建后集群拓扑变化会使旧拓扑的扫描和清理失败，且不报告部分成功。
    /// </summary>
    [ClusterTransitionFact]
    public async Task TopologyChangeAfterConstruction_RejectsOldScanAndClearWithoutPartialSuccess()
    {
        using var discovery = new CSRedisClient(GetClusterSeedEndpoint());
        var initial = await WaitForStableTopologyAsync(discovery, _ => true,
            "等待 Redis Cluster 初始三主三从拓扑稳定超时。");
        var targetReplica = initial.Replicas
            .OrderBy(replica => replica.Id, StringComparer.Ordinal)
            .First();
        var oldMasterId = targetReplica.MasterId;
        var scanClients = initial.Masters
            .OrderBy(master => master.Id, StringComparer.Ordinal)
            .Select(master => new CSRedisClient(master.Endpoint))
            .ToArray();
        using var router = new CSRedisClient(initial.Masters[0].Endpoint);
        var separator = targetReplica.Endpoint.LastIndexOf(':');
        using var replicaClient = new RedisClient("127.0.0.1",
            int.Parse(targetReplica.Endpoint.Substring(separator + 1)));
        try
        {
            var prefix = "transition-item:" + Guid.NewGuid().ToString("N") + ":";
            var configuration = new CacheConfiguration("cs-cluster-transition-" + Guid.NewGuid().ToString("N"),
                "redis", topology: CacheTopology.RedisCluster);
            var cache = new CSRedisCacheManager(router, configuration, scanClients);
            for (var index = 0; index < 120; index++)
                await cache.SetAsync(prefix + index, index + 1);
            Assert.Equal(120, (await cache.GetByPrefixAsync<int>(prefix)).Count);

            Assert.True(replicaClient.Connect(5000), "无法直连待晋升的 Redis Cluster 副本。");
            await replicaClient.CallAsync("CLUSTER", new[] { "FAILOVER", "TAKEOVER" });

            var changed = await WaitForStableTopologyAsync(discovery,
                topology => topology.Masters.Any(master => master.Id == targetReplica.Id) &&
                            topology.Masters.All(master => master.Id != oldMasterId),
                "等待 Redis Cluster 完成副本 TAKEOVER、稳定覆盖 16384 槽位并更换主节点身份超时。");
            Assert.Contains(changed.Masters, master => master.Id == targetReplica.Id);
            Assert.DoesNotContain(changed.Masters, master => master.Id == oldMasterId);

            var prefixException = await Assert.ThrowsAnyAsync<Exception>(
                () => cache.GetByPrefixAsync<int>(prefix));
            Assert.IsType<InvalidOperationException>(prefixException);
            var clearException = await Assert.ThrowsAnyAsync<Exception>(() => cache.ClearAsync());
            Assert.IsType<InvalidOperationException>(clearException);

            var currentScanClients = changed.Masters
                .OrderBy(master => master.Id, StringComparer.Ordinal)
                .Select(master => new CSRedisClient(master.Endpoint))
                .ToArray();
            using var currentRouter = new CSRedisClient(changed.Masters[0].Endpoint);
            try
            {
                var currentCache = new CSRedisCacheManager(currentRouter, configuration, currentScanClients);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (true)
                {
                    try
                    {
                        Assert.Equal(120, (await currentCache.GetByPrefixAsync<int>(prefix)).Count);
                        break;
                    }
                    catch (InvalidOperationException) when (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250));
                    }
                }
            }
            finally
            {
                foreach (var client in currentScanClients) client.Dispose();
            }
        }
        finally
        {
            foreach (var client in scanClients) client.Dispose();
        }
    }

    /// <summary>
    /// 等待故障后的 Redis Cluster 主节点列表稳定。
    /// </summary>
    /// <returns>包含已提升主节点的异步任务。</returns>
    private static async Task<IReadOnlyList<ClusterMaster>> WaitForPromotedMastersAsync()
    {
        using var discovery = new CSRedisClient(SurvivingSeedEndpoint);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var node = discovery.Nodes.Values.First();
                using var connection = await node.GetAsync();
                var response = await connection.Value.CallAsync("CLUSTER", new[] { "NODES" });
                if (TryReadCurrentMasters(response is byte[] bytes
                        ? System.Text.Encoding.UTF8.GetString(bytes)
                        : response?.ToString(), out var masters))
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
    /// <param name="predicate">筛选条件。</param>
    /// <param name="timeoutMessage">等待超时时使用的错误信息。</param>
    private static async Task<ClusterTopologySnapshot> WaitForStableTopologyAsync(
        CSRedisClient discovery, Func<ClusterTopologySnapshot, bool> predicate, string timeoutMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string stableSignature = null;
        var stablePolls = 0;
        Exception lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var node = discovery.Nodes.Values.First();
                using var connection = await node.GetAsync();
                var response = await connection.Value.CallAsync("CLUSTER", new[] { "NODES" });
                var nodes = response is byte[] bytes
                    ? System.Text.Encoding.UTF8.GetString(bytes)
                    : response?.ToString();
                if (TryReadStableTopology(nodes, out var topology) && predicate(topology))
                {
                    if (string.Equals(stableSignature, topology.Signature, StringComparison.Ordinal))
                        stablePolls++;
                    else
                    {
                        stableSignature = topology.Signature;
                        stablePolls = 1;
                    }
                    if (stablePolls >= 2)
                        return topology;
                }
                else
                {
                    stableSignature = null;
                    stablePolls = 0;
                }
            }
            catch (Exception exception)
            {
                lastError = exception;
                stableSignature = null;
                stablePolls = 0;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new Xunit.Sdk.XunitException(timeoutMessage +
            (lastError == null ? string.Empty : " 最近错误: " + lastError.Message));
    }

    /// <summary>
    /// 解析 Redis Cluster 节点清单并读取当前拓扑。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <param name="topology">用于输出解析结果的拓扑快照。</param>
    /// <returns>如果节点清单满足拓扑条件，则返回 <see langword="true" />。</returns>
    private static bool TryReadStableTopology(string nodes, out ClusterTopologySnapshot topology)
    {
        topology = null;
        if (string.IsNullOrWhiteSpace(nodes))
            return false;

        var masters = new List<ClusterMaster>();
        var replicas = new List<ClusterReplica>();
        var covered = new bool[16384];
        var masterSlotSignatures = new List<string>();
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                return false;
            var flags = fields[2].Split(',');
            if (flags.Contains("fail") || flags.Contains("fail?") ||
                !string.Equals(fields[7], "connected", StringComparison.Ordinal))
                continue;

            var endpoint = fields[1].Split('@')[0];
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;
            if (flags.Contains("master"))
            {
                var slotRanges = new List<string>();
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
                    slotRanges.Add(token);
                    for (var slot = start; slot <= last; slot++)
                    {
                        if (covered[slot])
                            return false;
                        covered[slot] = true;
                    }
                }
                masters.Add(new ClusterMaster(fields[0], endpoint));
                masterSlotSignatures.Add(fields[0] + "@" + endpoint + ":" + string.Join(",", slotRanges));
            }
            else if (flags.Contains("slave") || flags.Contains("replica"))
            {
                if (string.IsNullOrWhiteSpace(fields[3]))
                    return false;
                replicas.Add(new ClusterReplica(fields[0], endpoint, fields[3]));
            }
        }

        var masterIds = new HashSet<string>(masters.Select(master => master.Id), StringComparer.Ordinal);
        if (masters.Count != 3 || masterIds.Count != masters.Count ||
            masters.Select(master => master.Endpoint).Distinct(StringComparer.Ordinal).Count() != masters.Count ||
            replicas.Count != 3 || replicas.Select(replica => replica.Id).Distinct(StringComparer.Ordinal).Count() != replicas.Count ||
            replicas.Select(replica => replica.Endpoint).Distinct(StringComparer.Ordinal).Count() != replicas.Count ||
            replicas.Any(replica => !masterIds.Contains(replica.MasterId)) ||
            masters.Any(master => replicas.Count(replica => replica.MasterId == master.Id) != 1) ||
            covered.Any(slot => !slot))
            return false;

        var signature = string.Join("|", masterSlotSignatures.OrderBy(value => value, StringComparer.Ordinal)) + "||" +
                        string.Join("|", replicas.Select(replica => replica.Id + "@" + replica.Endpoint + ">" + replica.MasterId)
                            .OrderBy(value => value, StringComparer.Ordinal));
        topology = new ClusterTopologySnapshot(masters, replicas, signature);
        return true;
    }

    /// <summary>
    /// 获取 Redis Cluster 集成测试使用的种子节点端点。
    /// </summary>
    /// <returns>Redis Cluster 种子节点端点。</returns>
    private static string GetClusterSeedEndpoint()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException("Redis Cluster 拓扑切换测试要求显式设置 ConnectionStrings__RedisConnection。")
            : configured;
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
