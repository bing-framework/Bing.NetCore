using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 仅在隔离 Redis 扫描门控测试开关启用时运行扫描用例。
/// </summary>
public sealed class RedisScanGateFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="RedisScanGateFactAttribute"/> 类型的实例。
    /// </summary>
    public RedisScanGateFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_SCAN_GATE_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要独立 Redis 测试端点和 RUN_CACHE_SCAN_GATE_TESTS=true。";
    }
}

/// <summary>
/// 仅在三主 Redis Cluster 扫描故障开关启用时运行测试。
/// </summary>
public sealed class ClusterScanInFlightFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterScanInFlightFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterScanInFlightFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase))
            Skip = "需要独占的三主 Redis Cluster 和 RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS=true。";
    }
}

/// <summary>
/// 验证 Redis Cluster SCAN 执行期间连接中断时错误会向上传播。
/// </summary>
public sealed class ClusterScanInFlightFailureTest
{
    /// <summary>
    /// 用于发现测试集群主节点的 Redis 端点。
    /// </summary>
    private static readonly string[] SeedEndpoints =
    {
        "127.0.0.1:7000", "127.0.0.1:7001", "127.0.0.1:7002"
    };

    /// <summary>
    /// SCAN 已执行但响应丢失时向调用方传播集群错误。
    /// </summary>
    [RedisScanGateFact]
    public async Task ScanResponseGate_HoldsCompletedScanUntilConnectionIsAborted()
    {
        var endpoint = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")
            ?.Split(',', 2)[0].Trim();
        Assert.NotNull(endpoint);
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new InvalidOperationException("Redis 测试连接必须包含 host:port 端点。");

        await using var gate = new RedisScanResponseGate(endpoint[..separator], port);
        using var client = new RedisClient(ProxyConnection(gate.Port));
        var scan = Task.Run(() => client.ScanAsync(0, "*", 10, ""));
        await gate.ScanResponseCaptured.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(scan.IsCompleted, scan.Exception?.ToString());

        gate.AbortHeldScanResponse();
        await gate.DisposeAsync();
        var error = await Record.ExceptionAsync(async () => await scan.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.NotNull(error);
        Assert.IsNotType<TimeoutException>(error);
    }

    /// <summary>
    /// SCAN 已执行但响应丢失时传播 Redis Cluster 故障。
    /// </summary>
    [ClusterScanInFlightFact]
    public async Task ClusterScan_ResponseLostAfterExecution_PropagatesFailure()
    {
        await using var gate = new RedisScanResponseGate("127.0.0.1", 7000);
        using var router = new RedisClient(SeedEndpoints
            .Select(endpoint => (ConnectionStringBuilder)endpoint).ToArray())
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        var scanClients = new[]
        {
            new RedisClient(ProxyConnection(gate.Port)),
            new RedisClient(SeedEndpoints[1]),
            new RedisClient(SeedEndpoints[2])
        };
        var configuration = new CacheConfiguration(
            "free-cluster-inflight-scan-" + Guid.NewGuid().ToString("N"),
            "redis", batchSize: 8, topology: CacheTopology.RedisCluster);
        var prefix = "inflight-scan:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 120).Select(index => prefix + index).ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        Task<int> scan = null;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();

        try
        {
            foreach (var client in scanClients)
            {
                client.Serialize = JsonConvert.SerializeObject;
                client.Deserialize = JsonConvert.DeserializeObject;
            }
            var cache = new FreeRedisCacheManager(router, configuration, scanClients);
            await cache.SetAllAsync(keys.Select((key, index) => new KeyValuePair<string, int>(key, index + 1))
                .ToDictionary(item => item.Key, item => item.Value));

            scan = Task.Run(() => ConsumeAsync(cache.GetByPrefixStreamAsync<int>(prefix)));
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
                catch { /* 主断言保留扫描异常。 */ }
            }
            foreach (var key in physicalKeys)
            {
                try { await router.DelAsync(new[] { key }); }
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
    /// 消费并统计命中的缓存条目。
    /// </summary>
    /// <returns>异步操作，完成后返回命中的缓存条目数量。</returns>
    /// <param name="entries">待消费的缓存条目流。</param>
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
    /// 创建连接到本地扫描代理的 Redis 连接配置。
    /// </summary>
    /// <param name="port">服务端口。</param>
    /// <returns>连接到本地代理端口的 Redis 连接配置。</returns>
    private static ConnectionStringBuilder ProxyConnection(int port) =>
        ConnectionStringBuilder.Parse("127.0.0.1:" + port +
            ",receiveTimeout=60000,sendTimeout=60000,retry=0,minPoolSize=1,maxPoolSize=1");
}
