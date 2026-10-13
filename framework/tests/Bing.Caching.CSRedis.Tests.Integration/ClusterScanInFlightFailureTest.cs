using System.Runtime.ExceptionServices;
using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// Redis Cluster SCAN 响应中断测试特性。
/// </summary>
/// <remarks>
/// 仅在启用 Redis 集成环境和 RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS=true 时运行测试。
/// </remarks>
public sealed class ClusterScanInFlightFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterScanInFlightFactAttribute"/> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 根据集成环境和 RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS 环境变量决定是否跳过测试。
    /// </remarks>
    public ClusterScanInFlightFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase))
            Skip = "需要独占的三主 Redis Cluster 和 RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS=true。";
    }
}

/// <summary>
/// SCAN 响应门控测试特性。
/// </summary>
/// <remarks>
/// 仅在启用 Redis 集成环境和 RUN_CACHE_SCAN_GATE_TESTS=true 时运行测试。
/// </remarks>
public sealed class CSRedisScanGateFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="CSRedisScanGateFactAttribute"/> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 根据集成环境和 RUN_CACHE_SCAN_GATE_TESTS 环境变量决定是否跳过测试。
    /// </remarks>
    public CSRedisScanGateFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_SCAN_GATE_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase))
            Skip = "需要独立 Redis 测试端点和 RUN_CACHE_SCAN_GATE_TESTS=true。";
    }
}

/// <summary>
/// Redis Cluster SCAN 响应中断故障测试。
/// </summary>
/// <remarks>
/// 验证 Redis 已执行 SCAN 但客户端连接随后丢失时，CSRedis 会显式传播失败。
/// </remarks>
public sealed class ClusterScanInFlightFailureTest
{
    /// <summary>
    /// 保存三主 Redis Cluster 测试节点的初始端点，用于创建路由客户端并固定各扫描客户端。
    /// </summary>
    private static readonly string[] SeedEndpoints =
    {
        "127.0.0.1:7000", "127.0.0.1:7001", "127.0.0.1:7002"
    };

    /// <summary>
    /// 验证 SCAN 连接中断后的失败传播。
    /// </summary>
    /// <remarks>
    /// 代理捕获完整响应时扫描任务应保持未完成，释放连接后应向调用方传播故障。
    /// </remarks>
    [CSRedisScanGateFact]
    public async Task ScanResponseGate_HoldsCompletedScanUntilConnectionIsAborted()
    {
        var endpoint = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")
            ?.Split(',', 2)[0].Trim();
        Assert.NotNull(endpoint);
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new InvalidOperationException("Redis 测试连接必须包含 host:port 端点。");

        await using var gate = new RedisScanResponseGate(endpoint[..separator], port);
        using var client = new CSRedisClient("127.0.0.1:" + gate.Port);
        var scan = Task.Run(async () =>
        {
            using var connection = await client.Nodes.Values.Single().GetAsync();
            return await connection.Value.ScanAsync(0, "*", 10);
        });
        await gate.ScanResponseCaptured.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(scan.IsCompleted, scan.Exception?.ToString());

        gate.AbortHeldScanResponse();
        await gate.DisposeAsync();
        var error = await Record.ExceptionAsync(async () => await scan.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.NotNull(error);
        Assert.IsNotType<TimeoutException>(error);
    }

    /// <summary>
    /// 验证 Redis Cluster 流式扫描的响应丢失处理。
    /// </summary>
    /// <remarks>
    /// SCAN 请求在服务端执行后丢失响应时，流式前缀扫描应传播故障并完成资源清理。
    /// </remarks>
    [ClusterScanInFlightFact]
    public async Task ClusterScan_ResponseLostAfterExecution_PropagatesFailure()
    {
        await using var gate = new RedisScanResponseGate("127.0.0.1", 7000);
        using var router = new CSRedisClient(SeedEndpoints[0]);
        var scanClients = new[]
        {
            new CSRedisClient("127.0.0.1:" + gate.Port),
            new CSRedisClient(SeedEndpoints[1]),
            new CSRedisClient(SeedEndpoints[2])
        };
        PinScanClient(scanClients[0], "127.0.0.1:" + gate.Port);
        PinScanClient(scanClients[1], SeedEndpoints[1]);
        PinScanClient(scanClients[2], SeedEndpoints[2]);
        var configuration = new CacheConfiguration(
            "cs-cluster-inflight-scan-" + Guid.NewGuid().ToString("N"),
            "redis", batchSize: 8, topology: CacheTopology.RedisCluster);
        var prefix = "inflight-scan:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        Task<int> scan = null;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();

        try
        {
            var cache = new CSRedisCacheManager(router, configuration, scanClients);
            await cache.SetAllAsync(keys.Select((key, index) =>
                new KeyValuePair<string, int>(key, index + 1)).ToDictionary(item => item.Key, item => item.Value));

            // CSRedis 的 SCAN 调用可能在返回 Task 前执行同步准备工作，放入线程池后再等待代理门控。
            scan = Task.Run(() => ConsumeAsync(cache.GetByPrefixStreamAsync<int>(prefix)));
            var first = await Task.WhenAny(scan, gate.ScanResponseCaptured)
                .WaitAsync(TimeSpan.FromSeconds(30));
            if (first == scan)
            {
                var count = await scan;
                throw new InvalidOperationException("Cluster 流式扫描在代理截获 SCAN 响应前就已完成，条目数：" + count);
            }
            await gate.ScanResponseCaptured;
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
                catch { /* 主断言保留扫描异常。 */ }
            }
            foreach (var key in physicalKeys)
            {
                try { await router.DelAsync(key); }
                catch (Exception error) { cleanupErrors.Add(error); }
            }
            foreach (var client in scanClients)
                client.Dispose();
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
    /// 消费流式缓存条目并统计命中项。
    /// </summary>
    /// <remarks>
    /// 每个消费到的条目都必须命中。
    /// </remarks>
    /// <param name="entries">待消费的缓存条目异步流。</param>
    /// <returns>已消费且命中的缓存条目数量。</returns>
    private static async Task<int> ConsumeAsync(IAsyncEnumerable<CacheEntry<int>> entries)
    {
        var count = 0;
        await foreach (var entry in entries)
        {
            Assert.True(entry.Hit);
            count++;
        }
        return count;
    }

    /// <summary>
    /// 固定扫描客户端使用指定 Redis 节点。
    /// </summary>
    /// <remarks>
    /// 从客户端节点集合中移除其他端点，并断言最终只保留目标节点。
    /// </remarks>
    /// <param name="client">待裁剪节点集合的 CSRedis 客户端。</param>
    /// <param name="endpoint">要保留的 Redis host:port 端点。</param>
    private static void PinScanClient(CSRedisClient client, string endpoint)
    {
        foreach (var entry in client.Nodes.ToArray())
        {
            if (entry.Key.StartsWith(endpoint + "/", StringComparison.Ordinal))
                continue;
            if (client.Nodes.TryRemove(entry.Key, out var unused))
                unused.Dispose();
        }
        Assert.Single(client.Nodes);
    }
}
