#nullable enable

using Bing.Caching;
using Bing.Test.Shared;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching Redis Cluster 扫描在途与主节点重启组合故障门控。
/// </summary>
public sealed class EasyClusterScanRestartFactAttribute : FactAttribute
{
    /// <summary>
    /// 限制 Redis Cluster 扫描与重启测试容器名称的前缀。
    /// </summary>
    private const string ContainerPrefix = "bing-cache-cluster-092-";

    /// <summary>
    /// 初始化一个 <see cref="EasyClusterScanRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterScanRestartFactAttribute()
    {
        var container = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_SCAN_RESTART_CONTAINER");
        var port = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_SCAN_RESTART_PORT");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_SCAN_RESTART_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(container) ||
            !container.StartsWith(ContainerPrefix, StringComparison.Ordinal) ||
            !int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort) ||
            parsedPort is < 7000 or > 7002)
        {
            Skip = "需要显式允许重启且仅指向 bing-cache-cluster-092- 前缀的临时 Redis Cluster 主节点。";
        }
    }
}

/// <summary>
/// 验证 Redis Cluster 扫描在主节点重启期间的失败传播。
/// </summary>
/// <remarks>
/// 在途响应失败后应显式报错，并验证恢复后可重建 Manager。
/// </remarks>
public sealed class EasyClusterScanRestartIntegrationTest
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
    /// Cluster 扫描响应保持在途时重启主节点会传播失败，并支持恢复后重建 Manager。
    /// </summary>
    [EasyClusterScanRestartFact]
    public async Task RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure()
    {
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_SCAN_RESTART_CONTAINER");
        var targetPort = int.Parse(
            Environment.GetEnvironmentVariable("BING_TEST_CLUSTER_SCAN_RESTART_PORT")!,
            NumberStyles.Integer, CultureInfo.InvariantCulture);
        Assert.StartsWith("bing-cache-cluster-092-", containerName, StringComparison.Ordinal);

        await using var gate = new RedisScanResponseGate(Host, targetPort, IPAddress.Any);
        using var admin = await ConnectClusterAsync();
        using var targetControl = await ConnectNodeAsync(targetPort);
        var clusterMasters = ClusterPorts
            .Select(port => (Server: GetClusterServer(admin, port), ClientPort: port,
                BusPort: port + 10000))
            .ToArray();
        var originalClientPorts = clusterMasters
            .Select(node => node.Server.ConfigGet("cluster-announce-port").Single().Value.ToString())
            .ToArray();
        Assert.Equal(ClusterPorts.Select(port => port.ToString(CultureInfo.InvariantCulture)),
            originalClientPorts);

        var configuration = new CacheConfiguration(
            "easy-cluster-scan-restart-" + Guid.NewGuid().ToString("N"),
            "easy-cluster-scan-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 8, maxConcurrency: 4, topology: CacheTopology.RedisCluster);
        var otherConfiguration = new CacheConfiguration(
            configuration.Namespace + "-other", configuration.ProviderName,
            topology: CacheTopology.RedisCluster);
        var prefix = "cluster-scan-restart:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120)
            .Select(index => prefix + index.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var values = keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        var sentinel = CacheKeyFormat.GetPhysicalKey(otherConfiguration, "sentinel");
        Assert.True(physicalKeys.Select(CacheClusterTopology.GetHashSlot).Distinct().Count() > 1,
            "组合故障测试必须覆盖多个 Redis Cluster 槽位。");

        Task<int>? scan = null;
        var observedCount = 0;
        var changedAnnouncement = false;
        var restarted = false;
        var cleanupErrors = new List<Exception>();
        Exception? testError = null;

        try
        {
            using (var initial = BuildClusterCache(configuration, 7000))
            {
                var initialCache = initial.GetRequiredService<IRedisCache>();
                var database = GetDatabase(initial, configuration.ProviderName);
                await database.StringSetAsync(sentinel, "outside");
                await initialCache.SetAllAsync(values);
                Assert.Equal(values.Count, (await initialCache.GetByPrefixAsync<int>(prefix)).Count);

                var targetServer = targetControl.GetServer(Host, targetPort);
                var acknowledgedReplicas = (long)await targetServer.ExecuteAsync("WAIT", 1, 5000);
                Assert.True(acknowledgedReplicas >= 1,
                    "重叠故障测试必须先确认目标主节点的写入已被至少一个副本确认。");
            }

            await WaitForStableClusterAsync(admin);
            var oldRunId = await ReadRunIdAsync(targetControl.GetServer(Host, targetPort));

            changedAnnouncement = true;
            foreach (var node in clusterMasters)
                node.Server.Execute("CONFIG", "SET", "cluster-announce-port", gate.Port);
            foreach (var node in clusterMasters)
                await WaitForAnnouncementAsync(node.Server, gate.Port, node.BusPort);

            using (var scanningContainer = BuildClusterCache(configuration, gate.Port))
            {
                var cache = scanningContainer.GetRequiredService<IRedisCache>();
                scan = Task.Run(async () =>
                {
                    await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                    {
                        Assert.True(entry.Hit);
                        Interlocked.Increment(ref observedCount);
                    }

                    return observedCount;
                });

                var first = await Task.WhenAny(scan, gate.ScanResponseCaptured)
                    .WaitAsync(TimeSpan.FromSeconds(30));
                if (first == scan)
                    throw new InvalidOperationException("Cluster 扫描在闸门截获响应前已结束。", scan.Exception);

                await gate.ScanResponseCaptured;
                Assert.False(scan.IsCompleted, "完整 SCAN 响应被挂起时，流式读取不得完成。");

                await RunDockerAsync("restart", "--time", "1", containerName!);
                restarted = true;
                var newRunId = await WaitForRunIdChangeAsync(
                    targetControl.GetServer(Host, targetPort), oldRunId, TimeSpan.FromSeconds(45));
                Assert.NotEqual(oldRunId, newRunId);

                gate.AbortHeldScanResponse();
                var scanError = await Record.ExceptionAsync(async () =>
                    await scan.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.NotNull(scanError);
                Assert.IsNotType<TimeoutException>(scanError);
                Assert.True(observedCount < values.Count,
                    "节点重启后扫描不应报告完整数据集。");
            }

            await RestoreAnnouncementsAsync(clusterMasters, originalClientPorts);
            changedAnnouncement = false;
            await WaitForStableClusterAsync(admin);

            using var rebuilt = BuildClusterCache(configuration, 7000);
            var rebuiltCache = rebuilt.GetRequiredService<IRedisCache>();
            var requested = keys.Reverse().Take(80)
                .Concat(new[] { keys[0], prefix + "missing" })
                .ToArray();
            var expected = requested.Select(key =>
                values.TryGetValue(key, out var value) ? value : 0);
            Assert.Equal(expected, await rebuiltCache.GetAllAsync<int>(requested));
            Assert.Equal(values.Count, (await rebuiltCache.GetByPrefixAsync<int>(prefix)).Count);

            var streamed = new List<CacheEntry<int>>();
            await foreach (var entry in rebuiltCache.GetByPrefixStreamAsync<int>(prefix))
                streamed.Add(entry);
            Assert.Equal(values.Count, streamed.Count);
            Assert.All(streamed, entry => Assert.True(entry.Hit));

            await rebuiltCache.ClearAsync();
            Assert.Empty(await rebuiltCache.GetByPrefixAsync<int>(prefix));
            Assert.True(await GetDatabase(rebuilt, configuration.ProviderName)
                .KeyExistsAsync(sentinel));
        }
        catch (Exception error)
        {
            testError = error;
        }
        finally
        {
            gate.AbortHeldScanResponse();
            if (scan != null)
            {
                try
                {
                    await scan.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch
                {
                    // 保留主断言异常。
                }
            }

            if (changedAnnouncement)
            {
                try
                {
                    await RestoreAnnouncementsAsync(clusterMasters, originalClientPorts);
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }
            }

            try
            {
                await WaitForStableClusterAsync(admin);
            }
            catch (Exception error)
            {
                cleanupErrors.Add(error);
            }

            foreach (var physicalKey in physicalKeys.Append(sentinel))
            {
                try
                {
                    await admin.GetDatabase().KeyDeleteAsync(physicalKey);
                }
                catch (Exception error)
                {
                    cleanupErrors.Add(error);
                }
            }
        }

        if (testError != null && cleanupErrors.Count > 0)
            testError.Data["ClusterScanRestartCleanupError"] =
                new AggregateException(cleanupErrors).ToString();
        if (testError != null)
            throw new Xunit.Sdk.XunitException("Cluster 扫描与节点重启组合门禁失败。", testError);
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Cluster 扫描与节点重启组合测试清理失败。", cleanupErrors);
        Assert.True(restarted, "组合门禁必须实际重启目标 Redis 容器。");
    }

    /// <summary>
    /// 创建绑定指定端口和配置的 Redis Cluster 服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="firstMasterPort">首个主节点客户端端口。</param>
    /// <returns>返回已注册 Redis Cluster Provider 的服务容器。</returns>
    private static ServiceProvider BuildClusterCache(CacheConfiguration configuration,
        int firstMasterPort)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
        {
            foreach (var port in new[] { firstMasterPort }.Concat(ClusterPorts).Distinct())
                options.DBConfig.Endpoints.Add(new ServerEndPoint(Host, port));
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 获取测试容器中指定 Provider 使用的 Redis 数据库。
    /// </summary>
    /// <param name="container">测试服务容器。</param>
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
    /// 获取指定端口对应的 Redis Cluster 服务端。
    /// </summary>
    /// <param name="multiplexer">Redis 多路复用器。</param>
    /// <param name="port">Redis 节点客户端端口。</param>
    /// <returns>返回指定端口对应的 Redis 服务端。</returns>
    private static IServer GetClusterServer(ConnectionMultiplexer multiplexer, int port)
    {
        foreach (var endpoint in multiplexer.GetEndPoints().Where(candidate => candidate switch
        {
            System.Net.IPEndPoint ip => ip.Port == port,
            System.Net.DnsEndPoint dns => dns.Port == port,
            _ => false
        }))
        {
            try
            {
                var server = multiplexer.GetServer(endpoint);
                if (server.IsConnected)
                    return server;
            }
            catch (ArgumentException)
            {
                // 拓扑刷新期间旧种子端点可能已被替换，继续查找。
            }
        }

        throw new InvalidOperationException("Redis Cluster 中找不到已连接的节点端点：" + port);
    }

    /// <summary>
    /// 等待 Redis 节点宣布指定客户端和总线端口。
    /// </summary>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="port">Redis 节点客户端端口。</param>
    /// <param name="busPort">Redis Cluster 总线端口。</param>
    private static async Task WaitForAnnouncementAsync(IServer server, int port, int busPort)
    {
        var announced = ":" + port + "@" + busPort;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var nodes = (await server.ExecuteAsync("CLUSTER", "NODES")).ToString() ?? string.Empty;
                if (nodes.Contains(announced, StringComparison.Ordinal))
                    return;
            }
            catch (RedisConnectionException)
            {
                // 等待公告连接恢复。
            }
            catch (RedisTimeoutException)
            {
                // 等待公告命令响应恢复。
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Redis Cluster 未及时公告扫描闸门端口。");
    }

    /// <summary>
    /// 恢复 Redis Cluster 节点原有的宣布端口配置。
    /// </summary>
    /// <param name="clusterMasters">集群主节点及其当前公告端口。</param>
    /// <param name="originalClientPorts">各主节点原有的客户端公告端口。</param>
    private static async Task RestoreAnnouncementsAsync(
        IReadOnlyList<(IServer Server, int ClientPort, int BusPort)> clusterMasters,
        IReadOnlyList<string> originalClientPorts)
    {
        var errors = new List<Exception>();
        for (var index = 0; index < clusterMasters.Count; index++)
        {
            try
            {
                clusterMasters[index].Server.Execute(
                    "CONFIG", "SET", "cluster-announce-port", originalClientPorts[index]);
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("恢复 Redis Cluster 公告端口失败。", errors);

        for (var index = 0; index < clusterMasters.Count; index++)
            await WaitForAnnouncementAsync(clusterMasters[index].Server,
                int.Parse(originalClientPorts[index], CultureInfo.InvariantCulture),
                clusterMasters[index].BusPort);
    }

    /// <summary>
    /// 等待 Redis Cluster 的主从节点连接稳定。
    /// </summary>
    /// <param name="multiplexer">Redis 多路复用器。</param>
    private static async Task WaitForStableClusterAsync(ConnectionMultiplexer multiplexer)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                foreach (var endpoint in multiplexer.GetEndPoints())
                {
                    var server = multiplexer.GetServer(endpoint);
                    if (!server.IsConnected)
                        continue;
                    var info = (await server.ExecuteAsync("CLUSTER", "INFO")).ToString() ?? string.Empty;
                    var nodes = (await server.ExecuteAsync("CLUSTER", "NODES")).ToString() ?? string.Empty;
                    if (!info.Contains("cluster_state:ok", StringComparison.Ordinal) ||
                        !info.Contains("cluster_slots_assigned:16384", StringComparison.Ordinal) ||
                        !info.Contains("cluster_size:3", StringComparison.Ordinal))
                        continue;

                    var counts = CountConnectedNodes(nodes);
                    if (counts.Masters == 3 && counts.Replicas == 3)
                        return;
                }
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
            "等待 Redis Cluster 扫描故障组合后的完整拓扑超时。" +
            (lastError == null ? string.Empty : " 最近错误：" + lastError.Message));
    }

    /// <summary>
    /// 统计拓扑文本中的已连接主节点和副本节点。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <returns>返回已连接主节点和副本节点的数量。</returns>
    private static (int Masters, int Replicas) CountConnectedNodes(string nodes)
    {
        var masters = 0;
        var replicas = 0;
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8 || !string.Equals(fields[7], "connected", StringComparison.Ordinal))
                continue;
            var flags = fields[2].Split(',');
            if (flags.Contains("master", StringComparer.Ordinal) &&
                !flags.Contains("fail", StringComparer.Ordinal) &&
                !flags.Contains("fail?", StringComparer.Ordinal))
                masters++;
            else if ((flags.Contains("slave", StringComparer.Ordinal) ||
                      flags.Contains("replica", StringComparer.Ordinal)) &&
                     !flags.Contains("fail", StringComparer.Ordinal) &&
                     !flags.Contains("fail?", StringComparer.Ordinal))
                replicas++;
        }

        return (masters, replicas);
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
                // 等待重启后的连接恢复。
            }
            catch (RedisTimeoutException)
            {
                // 等待重启后的 INFO 响应恢复。
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Redis 主节点重启后未观察到新的 run_id。");
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
}
