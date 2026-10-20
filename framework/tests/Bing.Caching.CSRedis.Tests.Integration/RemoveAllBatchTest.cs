using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// RemoveAllAsync 批量删除测试。
/// </summary>
/// <remarks>
/// 使用 Redis MONITOR 验证数据键和租约键按配置批次删除。
/// </remarks>
public sealed class RemoveAllBatchTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于写入和校验数据键与租约键并发送监控标记的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="RemoveAllBatchTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供批量删除测试创建缓存管理器、写入租约键并校验删除结果的 Redis 客户端。</param>
    public RemoveAllBatchTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证 RemoveAllAsync 按 batchSize 批量删除数据和租约键。
    /// </summary>
    /// <remarks>
    /// 删除命令应符合配置的批次数量，并保留兄弟命名空间的哨兵键。
    /// </remarks>
    [IntegrationFact]
    public async Task RemoveAllAsync_UsesBatchSizeAndDeletesDataAndLeaseKeys()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("remove-all-batch-" + suffix, "csredis",
            batchSize: 2, maxConcurrency: 2);
        var cache = new CSRedisCacheManager(_client, configuration);
        var sibling = new CSRedisCacheManager(_client,
            new CacheConfiguration("remove-all-batch-sibling-" + suffix, "csredis", batchSize: 2));
        var keys = Enumerable.Range(0, 5)
            .Select(index => "remove-all-item:" + suffix + ":" + index)
            .ToArray();

        try
        {
            for (var index = 0; index < keys.Length; index++)
            {
                await cache.SetAsync(keys[index], index);
                _client.Set(CacheKeyFormat.GetLeaseKey(configuration, keys[index]), "lease");
            }
            sibling.Set("sentinel", 42);

            await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
            await cache.RemoveAllAsync(keys);

            var marker = "remove-all-batch-observed-" + suffix;
            await PingMarkerAsync(marker);
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(marker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var namespaceMarker = GetNamespaceMarker(configuration);
            var deletes = monitor.ObservedLines.Count(line =>
                line.Contains("\"del\"", StringComparison.OrdinalIgnoreCase) &&
                line.Contains(namespaceMarker, StringComparison.Ordinal));
            Assert.Equal(3, deletes);

            foreach (var key in keys)
            {
                Assert.False(_client.Exists(CacheKeyFormat.GetPhysicalKey(configuration, key)));
                Assert.False(_client.Exists(CacheKeyFormat.GetLeaseKey(configuration, key)));
            }
            Assert.Equal(42, sibling.Get<int>("sentinel"));
        }
        finally
        {
            await cache.RemoveAllAsync(keys);
            sibling.Clear();
        }
    }

    /// <summary>
    /// 发送用于界定 MONITOR 删除记录的 PING 标记。
    /// </summary>
    /// <param name="marker">写入 PING 消息的测试标记。</param>
    private async Task PingMarkerAsync(string marker)
    {
        // CSRedisClient.PingAsync(string) 的参数是路由键，不会作为 PING 消息发出。
        using var connection = await _client.Nodes.First().Value.GetAsync();
        await connection.Value.CallAsync("PING", marker);
    }

    /// <summary>
    /// 生成命名空间物理键的 MONITOR 识别前缀。
    /// </summary>
    /// <param name="configuration">包含命名空间和 Provider 名称的缓存配置。</param>
    /// <returns>按物理命名空间格式编码的前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";
}
