using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 扫描删除批次测试。
/// </summary>
/// <remarks>
/// 启用真实 Redis 后验证数据键和租约键同时清理，且只影响当前命名空间。
/// </remarks>
public sealed class ScanDeleteBatchTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于写入和校验数据键与租约键的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="ScanDeleteBatchTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供扫描删除测试创建缓存管理器、写入租约键并校验删除结果的 Redis 客户端。</param>
    public ScanDeleteBatchTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证扫描删除路径同时移除数据键和租约键。
    /// </summary>
    /// <remarks>
    /// 覆盖按前缀、按模式和清理路径，并检查兄弟命名空间保持不变。
    /// </remarks>
    [Fact]
    public async Task ScanDelete_RemovesDataAndLeaseKeysAcrossBatchedPrefixPatternAndClear()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("scan-delete-" + suffix, "csredis",
            batchSize: 2, maxConcurrency: 2);
        var cache = new CSRedisCacheManager(_client, configuration);
        var sibling = new CSRedisCacheManager(_client,
            new CacheConfiguration("scan-delete-sibling-" + suffix, "csredis", batchSize: 2));

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
    /// 验证取消扫描删除后不调度后续删除。
    /// </summary>
    /// <remarks>
    /// 首次删除响应被门控捕获并释放后，取消操作不得再发出下一次删除命令。
    /// </remarks>
    [IntegrationFact]
    public async Task RemoveByPrefixAsync_CancellationAfterFirstDelete_DoesNotDispatchNextDelete()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("scan-delete-cancel-" + suffix, "csredis",
            batchSize: 1, maxConcurrency: 1);
        var prefix = "scan-delete-cancel-item:" + suffix + ":";
        var keys = Enumerable.Range(0, 4).Select(index => prefix + index).ToArray();
        var operation = default(Task);
        var namespaceMarker = GetNamespaceMarker(configuration);
        var (host, port) = ParseEndpoint(connection);
        await using var gate = new RedisDeleteResponseGate(host, port, namespaceMarker);
        using var gatedClient = new CSRedisClient(ProxyConnection(connection, gate.Port));
        var cache = new CSRedisCacheManager(gatedClient, configuration);

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
    /// 写入扫描删除测试所需的数据键和租约键。
    /// </summary>
    /// <param name="cache">用于写入数据键的缓存管理器。</param>
    /// <param name="configuration">用于生成数据键和租约键的缓存配置。</param>
    /// <param name="keys">待写入的逻辑缓存键集合。</param>
    private async Task SeedAsync(CSRedisCacheManager cache, CacheConfiguration configuration,
        IReadOnlyList<string> keys)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            await cache.SetAsync(keys[index], index);
            _client.Set(CacheKeyFormat.GetLeaseKey(configuration, keys[index]), "lease");
        }
    }

    /// <summary>
    /// 断言逻辑键对应的物理键已删除。
    /// </summary>
    /// <remarks>
    /// 同时检查数据键和对应租约键均不存在。
    /// </remarks>
    /// <param name="configuration">用于生成物理键的缓存配置。</param>
    /// <param name="keys">需要检查删除结果的逻辑缓存键集合。</param>
    private void AssertDeleted(CacheConfiguration configuration, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            Assert.False(_client.Exists(CacheKeyFormat.GetPhysicalKey(configuration, key)));
            Assert.False(_client.Exists(CacheKeyFormat.GetLeaseKey(configuration, key)));
        }
    }

    /// <summary>
    /// 生成命名空间物理键的 MONITOR 识别前缀。
    /// </summary>
    /// <param name="configuration">包含命名空间和 Provider 名称的缓存配置。</param>
    /// <returns>按物理命名空间格式编码的前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";

    /// <summary>
    /// 解析 Redis 连接串的首个端点。
    /// </summary>
    /// <remarks>
    /// 解析结果用于创建响应门控代理，并支持 redis:// 前缀。
    /// </remarks>
    /// <param name="connection">包含 host:port 的 Redis 连接串，可带 redis:// 前缀和连接选项。</param>
    /// <returns>解析得到的 Redis 主机和端口。</returns>
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
    /// 生成指向响应门控代理的 Redis 连接串。
    /// </summary>
    /// <remarks>
    /// 连接串指向本地代理端口，并保留原连接串中的附加选项。
    /// </remarks>
    /// <param name="connection">原始 Redis 连接串，用于提取连接选项。</param>
    /// <param name="port">响应门控代理监听的本地端口。</param>
    /// <returns>指向本地代理并保留原连接选项的 Redis 连接串。</returns>
    private static string ProxyConnection(string connection, int port)
    {
        var separator = connection.IndexOf(',');
        var options = separator >= 0 ? connection.Substring(separator) : string.Empty;
        return "127.0.0.1:" + port + options + ",poolsize=2";
    }
}
