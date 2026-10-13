using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// FreeRedis 批量删除测试。
/// </summary>
/// <remarks>
/// 使用 Redis MONITOR 验证 RemoveAllAsync 按配置批次同时删除数据键和租约键。
/// </remarks>
public sealed class RemoveAllBatchTest
{
    /// <summary>
    /// 用于准备批量删除测试数据的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="RemoveAllBatchTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public RemoveAllBatchTest(RedisClient client) => _client = client;

    /// <summary>
    /// 批量删除按配置的批次大小执行并同时删除数据键和租约键。
    /// </summary>
    [IntegrationFact]
    public async Task RemoveAllAsync_UsesBatchSizeAndDeletesDataAndLeaseKeys()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration("remove-all-batch-" + suffix, "freeredis",
            batchSize: 2, maxConcurrency: 2);
        var cache = new FreeRedisCacheManager(_client, configuration);
        var sibling = new FreeRedisCacheManager(_client,
            new CacheConfiguration("remove-all-batch-sibling-" + suffix, "freeredis", batchSize: 2));
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
            await _client.CallAsync(new CommandPacket("PING", marker));
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
    /// 生成用于筛选当前命名空间物理键的前缀。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>包含命名空间和 Provider 名称的物理键前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";
}
