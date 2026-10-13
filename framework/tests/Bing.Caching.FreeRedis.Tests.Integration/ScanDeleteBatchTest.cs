using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// FreeRedis 扫描删除批次集成测试。
/// </summary>
/// <remarks>
/// 使用真实 Redis 验证数据键和租约键同时清理，且只影响当前命名空间。
/// </remarks>
public sealed class ScanDeleteBatchTest
{
    /// <summary>
    /// 用于准备扫描和批量删除测试数据的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="ScanDeleteBatchTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public ScanDeleteBatchTest(RedisClient client) => _client = client;

    /// <summary>
    /// 按前缀、模式和命名空间清理时分批删除数据键及租约键。
    /// </summary>
    [Fact]
    public async Task ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("scan-delete-" + suffix, "freeredis",
            batchSize: 2, maxConcurrency: 2);
        var cache = new FreeRedisCacheManager(_client, configuration);
        var sibling = new FreeRedisCacheManager(_client,
            new CacheConfiguration("scan-delete-sibling-" + suffix, "freeredis", batchSize: 2));

        try
        {
            var prefixKeys = Enumerable.Range(0, 5).Select(index => "prefix:" + index).ToArray();
            await SeedAsync(cache, configuration, prefixKeys);
            cache.RemoveByPrefix("prefix:");
            AssertDeleted(configuration, prefixKeys);

            var patternKeys = Enumerable.Range(0, 5).Select(index => "pattern:" + index).ToArray();
            await SeedAsync(cache, configuration, patternKeys);
            await cache.RemoveByPatternAsync("pattern:*");
            AssertDeleted(configuration, patternKeys);

            var clearKeys = Enumerable.Range(0, 5).Select(index => "clear:" + index).ToArray();
            await SeedAsync(cache, configuration, clearKeys);
            sibling.Set("sentinel", 42);
            await cache.ClearAsync();
            AssertDeleted(configuration, clearKeys);
            Assert.Equal(42, sibling.Get<int>("sentinel"));
        }
        finally
        {
            await cache.ClearAsync();
            sibling.Clear();
        }
    }

    /// <summary>
    /// 前缀删除首个批次后取消时不派发下一条删除命令。
    /// </summary>
    [IntegrationFact]
    public async Task RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("scan-delete-cancel-" + suffix, "freeredis",
            batchSize: 1, maxConcurrency: 1);
        var prefix = "scan-delete-cancel-item:" + suffix + ":";
        var keys = Enumerable.Range(0, 4).Select(index => prefix + index).ToArray();
        var operation = default(Task);
        var namespaceMarker = GetNamespaceMarker(configuration);
        var (host, port) = ParseEndpoint(connection);
        await using var gate = new RedisDeleteResponseGate(host, port, namespaceMarker);
        using var gatedClient = new RedisClient(ProxyConnection(connection, gate.Port))
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        var cache = new FreeRedisCacheManager(gatedClient, configuration);

        try
        {
            await SeedAsync(cache, configuration, keys);
            using var cancellation = new CancellationTokenSource();
            operation = Task.Run(() => cache.RemoveByPrefixAsync(prefix, cancellation.Token));

            await gate.DeleteResponseCaptured.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, gate.TargetDeleteCount);

            cancellation.Cancel();
            gate.ReleaseHeldDeleteResponse();
            await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
            Assert.Equal(1, gate.TargetDeleteCount);
        }
        finally
        {
            gate.ReleaseHeldDeleteResponse();
            if (operation != null)
            {
                try
                {
                    await operation;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await cache.RemoveByPrefixAsync(prefix);
        }
    }

    /// <summary>
    /// 写入待测试的逻辑键及其租约键。
    /// </summary>
    /// <param name="cache">缓存实例。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="keys">逻辑缓存键集合。</param>
    private async Task SeedAsync(FreeRedisCacheManager cache, CacheConfiguration configuration,
        IReadOnlyList<string> keys)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            await cache.SetAsync(keys[index], index);
            _client.Set(CacheKeyFormat.GetLeaseKey(configuration, keys[index]), "lease");
        }
    }

    /// <summary>
    /// 验证指定逻辑键及其租约键均已删除。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="keys">逻辑缓存键集合。</param>
    private void AssertDeleted(CacheConfiguration configuration, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            Assert.False(_client.Exists(CacheKeyFormat.GetPhysicalKey(configuration, key)));
            Assert.False(_client.Exists(CacheKeyFormat.GetLeaseKey(configuration, key)));
        }
    }

    /// <summary>
    /// 生成用于筛选当前命名空间物理键的前缀。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>包含命名空间和 Provider 名称的物理键前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";

    /// <summary>
    /// 解析 Redis 连接串中的首个端点主机名和端口。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>解析出的 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        var endpoint = connection.Split(',', 2)[0].Trim();
        if (endpoint.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
            endpoint = endpoint.Substring("redis://".Length);
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint.Substring(separator + 1), out var port))
            throw new InvalidOperationException("Redis 测试连接必须包含 host:port 端点。");
        return (endpoint.Substring(0, separator), port);
    }

    /// <summary>
    /// 使用代理端口重建 Redis 连接串并限制连接池。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="port">服务端口。</param>
    /// <returns>配置了固定端口和有限连接池的本地代理 Redis 连接串。</returns>
    private static string ProxyConnection(string connection, int port)
    {
        var separator = connection.IndexOf(',');
        var options = separator >= 0 ? connection.Substring(separator) : string.Empty;
        return "127.0.0.1:" + port + options +
               ",retry=0,minPoolSize=1,maxPoolSize=1,receiveTimeout=60000,sendTimeout=60000";
    }
}
