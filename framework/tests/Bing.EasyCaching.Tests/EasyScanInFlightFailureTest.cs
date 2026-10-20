using Bing.Caching;
using Bing.Test.Shared;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using StackExchange.Redis;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 仅在启用 Redis 扫描门控测试和独立 Redis 端点时运行。
/// </summary>
public sealed class EasyScanGateFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyScanGateFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyScanGateFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_SCAN_GATE_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")))
            Skip = "需要独立 Redis 测试端点和 RUN_CACHE_SCAN_GATE_TESTS=true。";
    }
}

/// <summary>
/// 仅在启用集成测试、Cluster 扫描故障开关和公告地址变更许可时运行。
/// </summary>
public sealed class EasyClusterScanGateFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyClusterScanGateFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyClusterScanGateFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_CLUSTER_ANNOUNCE_CHANGE"),
                "true", StringComparison.OrdinalIgnoreCase))
            Skip = "需要独占的三主 Redis Cluster，并显式允许临时修改节点公告端口。";
    }
}

/// <summary>
/// 验证 Redis Cluster 扫描响应丢失时的失败传播。
/// </summary>
public sealed class EasyScanInFlightFailureTest
{
    /// <summary>
    /// Cluster 扫描响应执行后丢失时传播失败，而不是返回伪成功。
    /// </summary>
    [EasyClusterScanGateFact]
    public async Task RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure()
    {
        await using var gate = new RedisScanResponseGate("127.0.0.1", 7000, IPAddress.Any);
        using var admin = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:7000,allowAdmin=true");
        var master = GetClusterServer(admin, 7000);
        var clusterMasters = new[]
        {
            (Server: master, ClientPort: 7000, BusPort: 17000),
            (Server: GetClusterServer(admin, 7001), ClientPort: 7001, BusPort: 17001),
            (Server: GetClusterServer(admin, 7002), ClientPort: 7002, BusPort: 17002)
        };
        var originalClientPorts = clusterMasters
            .Select(node => node.Server.ConfigGet("cluster-announce-port").Single().Value.ToString())
            .ToArray();
        Assert.Equal("7000", originalClientPorts[0]);
        Assert.Equal("7001", originalClientPorts[1]);
        Assert.Equal("7002", originalClientPorts[2]);

        var configuration = new CacheConfiguration(
            "easy-cluster-inflight-scan-" + Guid.NewGuid().ToString("N"),
            "easy-cluster-scan-gate", batchSize: 8, topology: CacheTopology.RedisCluster);
        var prefix = "inflight-scan:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var physicalKeys = keys.Select(key => (RedisKey)CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        Task scan = null;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();
        var changedAnnouncement = false;

        try
        {
            using (var seed = BuildClusterCache(configuration, 7000))
            {
                var cache = seed.GetRequiredService<IRedisCache>();
                await cache.SetAllAsync(keys.Select((key, index) =>
                    new KeyValuePair<string, int>(key, index + 1))
                    .ToDictionary(item => item.Key, item => item.Value));
            }

            changedAnnouncement = true;
            foreach (var node in clusterMasters)
                node.Server.Execute("CONFIG", "SET", "cluster-announce-port", gate.Port);
            foreach (var node in clusterMasters)
                await WaitForAnnouncementAsync(node.Server, gate.Port, node.BusPort);

            using var container = BuildClusterCache(configuration, gate.Port);
            var provider = container.GetRequiredService<global::EasyCaching.Core.IEasyCachingProviderFactory>()
                .GetCachingProvider(configuration.ProviderName);
            var database = Assert.IsAssignableFrom<IDatabase>(provider.Database);
            var endpoints = database.Multiplexer.GetEndPoints();
            Assert.Contains(endpoints, endpoint => endpoint is System.Net.IPEndPoint ip && ip.Port == gate.Port);

            var cacheToScan = container.GetRequiredService<IRedisCache>();
            scan = Task.Run(async () =>
            {
                var count = 0;
                await foreach (var entry in cacheToScan.GetByPrefixStreamAsync<int>(prefix))
                {
                    Assert.True(entry.Hit);
                    count++;
                }
                return count;
            });
            var first = await Task.WhenAny(scan, gate.ScanResponseCaptured)
                .WaitAsync(TimeSpan.FromSeconds(30));
            if (first == scan)
                throw new InvalidOperationException("Cluster 扫描在代理截获响应前已结束。", scan.Exception);
            await gate.ScanResponseCaptured;
            Assert.False(scan.IsCompleted, "完整 SCAN 响应暂存时，Cluster 流式读取不得完成。");
            gate.AbortHeldScanResponse();
            await gate.DisposeAsync();

            var error = await Record.ExceptionAsync(async () => await scan.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(error);
            Assert.IsNotType<TimeoutException>(error);
        }
        catch (Exception error)
        {
            testError = error;
        }
        finally
        {
            gate.AbortHeldScanResponse();
            await gate.DisposeAsync();
            if (scan != null)
            {
                try { await scan.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { /* 保留主断言异常。 */ }
            }
            if (changedAnnouncement)
            {
                for (var index = 0; index < clusterMasters.Length; index++)
                {
                    try
                    {
                        clusterMasters[index].Server.Execute(
                            "CONFIG", "SET", "cluster-announce-port", originalClientPorts[index]);
                    }
                    catch (Exception error) { cleanupErrors.Add(error); }
                }
            }
            foreach (var key in physicalKeys)
            {
                try { await admin.GetDatabase().KeyDeleteAsync(key); }
                catch (Exception error) { cleanupErrors.Add(error); }
            }
        }

        if (testError != null)
        {
            if (cleanupErrors.Count > 0)
                testError.Data["ClusterScanCleanupError"] = new AggregateException(cleanupErrors).ToString();
            ExceptionDispatchInfo.Capture(testError).Throw();
        }
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Cluster SCAN 故障测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 创建绑定指定首个主节点端口的 Redis Cluster 服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="firstMasterPort">首个主节点客户端端口。</param>
    /// <returns>返回已注册 Redis Cluster Provider 的服务容器。</returns>
    private static ServiceProvider BuildClusterCache(CacheConfiguration configuration, int firstMasterPort)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
        {
            foreach (var port in new[] { firstMasterPort, 7001, 7002 })
                options.DBConfig.Endpoints.Add(new ServerEndPoint("127.0.0.1", port));
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 等待 Redis 节点宣布指定客户端和总线端口。
    /// </summary>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="port">Redis 节点客户端端口。</param>
    /// <param name="busPort">Redis Cluster 总线端口。</param>
    private static async Task WaitForAnnouncementAsync(IServer server, int port, int busPort)
    {
        // Docker/NAT 环境的公告 IP 不固定；本门禁只要求客户端代理端口和总线端口匹配。
        var announced = ":" + port + "@" + busPort;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (server.Execute("CLUSTER", "NODES").ToString()
                .Contains(announced, StringComparison.Ordinal))
                return;
            await Task.Delay(100);
        }
        throw new TimeoutException("Redis Cluster 未及时公告扫描代理端口。");
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
                // 拓扑刷新期间旧种子端点可能已被替换，继续尝试公告端点。
            }
        }

        throw new InvalidOperationException("Redis Cluster 中找不到已连接的节点端点：" + port);
    }

    /// <summary>
    /// Redis 扫描响应执行后丢失时传播失败。
    /// </summary>
    [EasyScanGateFact]
    public async Task RedisScan_ResponseLostAfterExecution_PropagatesFailure()
    {
        var endpoint = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")
            .Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new InvalidOperationException("Redis 测试连接必须包含 host:port 端点。");

        var host = endpoint[..separator];
        await using var gate = new RedisScanResponseGate(host, port);
        using var direct = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var configuration = new CacheConfiguration(
            "easy-inflight-scan-" + Guid.NewGuid().ToString("N"),
            "easy-scan-gate", batchSize: 8);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.Endpoints.Add(new ServerEndPoint("127.0.0.1", gate.Port)));
        using var container = services.BuildServiceProvider();
        var cache = container.GetRequiredService<IRedisCache>();
        var prefix = "inflight-scan:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var physicalKeys = keys.Select(key => (RedisKey)CacheKeyFormat.GetPhysicalKey(configuration, key))
            .ToArray();
        Task scan = null;
        Exception testError = null;
        Exception cleanupError = null;

        try
        {
            await cache.SetAllAsync(keys.Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
                .ToDictionary(item => item.Key, item => item.Value));
            scan = Task.Run(async () =>
            {
                await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                    Assert.True(entry.Hit);
            });
            await gate.ScanResponseCaptured.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(scan.IsCompleted, "完整 SCAN 响应被代理暂存时，流式读取不得完成。");
            gate.AbortHeldScanResponse();
            await gate.DisposeAsync();

            var error = await Record.ExceptionAsync(async () => await scan.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(error);
            Assert.IsNotType<TimeoutException>(error);
        }
        catch (Exception error)
        {
            testError = error;
        }
        finally
        {
            gate.AbortHeldScanResponse();
            await gate.DisposeAsync();
            if (scan != null)
            {
                try { await scan.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { /* 保留主断言异常。 */ }
            }
            try { await direct.GetDatabase().KeyDeleteAsync(physicalKeys); }
            catch (Exception error) { cleanupError = error; }
        }

        if (testError != null)
        {
            if (cleanupError != null)
                testError.Data["ScanCleanupError"] = cleanupError.ToString();
            ExceptionDispatchInfo.Capture(testError).Throw();
        }
        if (cleanupError != null)
            throw cleanupError;
    }
}
