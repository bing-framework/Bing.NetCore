#nullable enable

using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching Redis Cluster 主节点重启门控。
/// </summary>
public sealed class EasyClusterRestartFactAttribute : FactAttribute
{
    /// <summary>
    /// 限制 Redis Cluster 主节点重启测试容器名称的前缀。
    /// </summary>
    private const string ContainerPrefix = "bing-cache-cluster-091-";

    /// <summary>
    /// 初始化一个 <see cref="EasyClusterRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterRestartFactAttribute()
    {
        var container = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_RESTART_CONTAINER");
        var port = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_RESTART_PORT");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_RESTART_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(container) ||
            !container.StartsWith(ContainerPrefix, StringComparison.Ordinal) ||
            !int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort) ||
            parsedPort is < 7000 or > 7002)
        {
            Skip = "需要显式允许重启且仅指向 bing-cache-cluster-091- 前缀的临时 Redis Cluster 主节点。";
        }
    }
}

/// <summary>
/// 验证 Redis Cluster 主节点重启后的访问恢复。
/// </summary>
/// <remarks>
/// EasyCaching Redis 应由新 Manager 恢复完整的跨槽访问。
/// </remarks>
public sealed class EasyClusterRestartIntegrationTest
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
    /// 主节点容器重启后由新 CacheManager 恢复跨槽批量访问。
    /// </summary>
    [EasyClusterRestartFact]
    public async Task RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots()
    {
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_RESTART_CONTAINER");
        var targetPort = int.Parse(
            Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_RESTART_PORT")!,
            NumberStyles.Integer, CultureInfo.InvariantCulture);
        Assert.StartsWith("bing-cache-cluster-091-", containerName, StringComparison.Ordinal);

        var configuration = new CacheConfiguration(
            "easy-cluster-restart-" + Guid.NewGuid().ToString("N"),
            "easy-cluster-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 16, maxConcurrency: 4, topology: CacheTopology.RedisCluster);
        var otherConfiguration = new CacheConfiguration(
            configuration.Namespace + "-other", configuration.ProviderName,
            topology: CacheTopology.RedisCluster);
        var prefix = "cluster-restart:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120)
            .Select(index => prefix + index.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var requested = keys.Reverse().Take(80)
            .Concat(new[] { keys[0], prefix + "missing" })
            .ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");

        Assert.True(physicalKeys.Select(CacheClusterTopology.GetHashSlot).Distinct().Count() > 1,
            "主节点重启测试必须覆盖多个 Redis Cluster 槽位。");

        using var control = await ConnectClusterAsync();
        using var targetControl = await ConnectNodeAsync(targetPort);
        using var initial = BuildRedis(configuration);
        var initialCache = initial.GetRequiredService<IRedisCache>();
        var database = GetDatabase(initial, configuration.ProviderName);

        try
        {
            var before = await WaitForStableTopologyAsync(control);
            Assert.Contains(before.Masters, node => node.Port == targetPort);
            var oldRunId = await ReadRunIdAsync(targetControl.GetServer(Host, targetPort));

            await database.StringSetAsync(sentinel, "outside");
            await initialCache.SetAllAsync(values);
            Assert.Equal(values.Count, (await initialCache.GetByPrefixAsync<int>(prefix)).Count);

            await RunDockerAsync("restart", "--time", "1", containerName!);
            var newRunId = await WaitForRunIdChangeAsync(targetControl.GetServer(Host, targetPort), oldRunId,
                TimeSpan.FromSeconds(45));
            Assert.NotEqual(oldRunId, newRunId);

            var after = await WaitForStableTopologyAsync(control);
            Assert.True(after.SlotsCovered);
            Assert.Equal(3, after.Masters.Count);
            Assert.Equal(3, after.Replicas.Count);

            using var rebuilt = BuildRedis(configuration);
            var cache = rebuilt.GetRequiredService<IRedisCache>();
            var expected = requested.Select(key =>
                values.TryGetValue(key, out var value) ? value : 0);
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
            Assert.True(await database.KeyExistsAsync(sentinel));
        }
        finally
        {
            foreach (var physicalKey in physicalKeys.Append(sentinel))
            {
                try
                {
                    await database.KeyDeleteAsync(physicalKey);
                }
                catch (RedisConnectionException)
                {
                    // 重启窗口中的旧连接可能短暂不可用；任务结束时会清理专用容器。
                }
                catch (RedisTimeoutException)
                {
                    // 重启窗口中的旧连接可能短暂超时；主断言优先。
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
    /// 执行指定的 Docker 控制命令并返回标准输出。
    /// </summary>
    /// <returns>异步操作，完成后返回 Docker 命令的标准输出。</returns>
    /// <param name="arguments">命令行参数。</param>
    private static async Task<string> RunDockerAsync(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start())
            throw new InvalidOperationException("无法启动 Docker 命令。");

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Docker 命令失败：{await error}");
        return await output;
    }

    /// <summary>
    /// 等待 Redis 节点运行标识变为不同于旧值的值。
    /// </summary>
    /// <returns>异步操作，完成后返回变化后的 Redis 节点运行标识。</returns>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="oldRunId">旧运行标识。</param>
    /// <param name="timeout">超时时间。</param>
    private static async Task<string> WaitForRunIdChangeAsync(IServer server, string oldRunId,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var current = await ReadRunIdAsync(server);
                if (!string.Equals(current, oldRunId, StringComparison.Ordinal))
                    return current;
            }
            catch (RedisConnectionException)
            {
                // 等待 Docker 重启后的连接恢复。
            }
            catch (RedisTimeoutException)
            {
                // 等待重启后的 INFO 响应恢复。
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Redis 主节点重启后未在限定时间内观察到新的 run_id。");
    }

    /// <summary>
    /// 读取 Redis 节点的运行标识。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis 节点运行标识。</returns>
    /// <param name="server">Redis 服务端。</param>
    private static async Task<string> ReadRunIdAsync(IServer server)
    {
        var info = (await server.ExecuteAsync("INFO", "SERVER")).ToString() ?? string.Empty;
        foreach (var line in info.Split('\n'))
        {
            if (!line.StartsWith("run_id:", StringComparison.Ordinal))
                continue;
            var runId = line["run_id:".Length..].Trim();
            if (!string.IsNullOrEmpty(runId))
                return runId;
        }

        throw new InvalidOperationException("Redis INFO SERVER 响应中缺少 run_id。");
    }

    /// <summary>
    /// 等待 Redis Cluster 达到完整且稳定的拓扑状态。
    /// </summary>
    /// <returns>异步操作，完成后返回稳定的 Redis Cluster 拓扑快照。</returns>
    /// <param name="multiplexer">Redis 多路复用器。</param>
    private static async Task<ClusterTopology> WaitForStableTopologyAsync(
        ConnectionMultiplexer multiplexer)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var topology = await ReadTopologyAsync(multiplexer);
                if (topology.IsStable)
                    return topology;
            }
            catch (RedisConnectionException error)
            {
                lastError = error;
            }
            catch (RedisTimeoutException error)
            {
                lastError = error;
            }

            await Task.Delay(250);
        }

        throw new Xunit.Sdk.XunitException(
            "等待 Redis Cluster 主节点重启后拓扑稳定超时。" +
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
                var nodes = (await server.ExecuteAsync("CLUSTER", "NODES")).ToString() ?? string.Empty;
                var topology = ParseTopology(nodes);
                if (topology != null)
                    return topology;
            }
            catch (RedisConnectionException)
            {
                // 节点重启期间尝试下一个已连接节点。
            }
            catch (RedisTimeoutException)
            {
                // 节点重启期间尝试下一个已连接节点。
            }
        }

        throw new InvalidOperationException("无法从 Redis Cluster 读取节点清单。");
    }

    /// <summary>
    /// 将 CLUSTER NODES 文本解析为拓扑快照。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <returns>返回解析后的 Redis Cluster 拓扑快照；无法解析时返回 null。</returns>
    private static ClusterTopology? ParseTopology(string nodes)
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
        internal ClusterNode(string id, int port, string? masterId)
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
        internal string? MasterId { get; }
    }
}
