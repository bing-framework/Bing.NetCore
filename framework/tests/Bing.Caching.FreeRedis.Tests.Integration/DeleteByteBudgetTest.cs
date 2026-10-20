using System.Text;
using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 使用 Redis MONITOR 验证删除命令按 UTF-8 字节预算切分。
/// </summary>
public sealed class DeleteByteBudgetTest
{
    /// <summary>
    /// 用于准备并检查删除预算测试数据的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="DeleteByteBudgetTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public DeleteByteBudgetTest(RedisClient client) => _client = client;

    /// <summary>
    /// 长 UTF-8 键按命令字节预算拆分删除并保留相邻命名空间数据。
    /// </summary>
    [IntegrationFact]
    public async Task RemoveAllAsync_SplitsLongUtf8KeysBeforeCommandBudgetAndPreservesSibling()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var namespaceName = "delete-budget-" + suffix;
        var providerName = "freeredis";
        var shape = new CacheConfiguration(namespaceName, providerName, batchSize: 16);
        var keys = Enumerable.Range(0, 4)
            .Select(index => "订单-😀-" + suffix + "-" + index.ToString("D2"))
            .ToArray();
        var pairBytes = GetPairBytes(CacheKeyFormat.GetPhysicalKey(shape, keys[0]));
        var configuration = new CacheConfiguration(namespaceName, providerName,
            batchSize: 16, maxConcurrency: 2,
            resources: new CacheResourceOptions(maxBatchCommandBytes: pairBytes * 2 - 1));
        var cache = new FreeRedisCacheManager(_client, configuration);
        var sibling = new FreeRedisCacheManager(_client,
            new CacheConfiguration(namespaceName + "-sibling", providerName));

        try
        {
            await SeedAsync(cache, configuration, keys);
            sibling.Set("sentinel", 42);

            await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
            await cache.RemoveAllAsync(keys);

            var marker = "delete-budget-observed-" + suffix;
            await _client.CallAsync(new CommandPacket("PING", marker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(marker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var namespaceMarker = GetNamespaceMarker(configuration);
            var deletes = monitor.Count(line =>
                line.Contains("\"del\"", StringComparison.OrdinalIgnoreCase) &&
                line.Contains(namespaceMarker, StringComparison.Ordinal));
            Assert.Equal(keys.Length, deletes);
            AssertDeleted(configuration, keys);
            Assert.Equal(42, sibling.Get<int>("sentinel"));
        }
        finally
        {
            await cache.RemoveAllAsync(keys);
            sibling.Clear();
        }
    }

    /// <summary>
    /// 单个键超过命令字节预算时，在派发删除命令前拒绝操作。
    /// </summary>
    [IntegrationFact]
    public async Task RemoveAllAsync_RejectsSingleKeyOverCommandBudgetBeforeDispatch()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var namespaceName = "delete-budget-limit-" + suffix;
        var providerName = "freeredis";
        var shape = new CacheConfiguration(namespaceName, providerName);
        var key = "超限-😀-" + suffix;
        var pairBytes = GetPairBytes(CacheKeyFormat.GetPhysicalKey(shape, key));
        var configuration = new CacheConfiguration(namespaceName, providerName,
            resources: new CacheResourceOptions(maxBatchCommandBytes: pairBytes - 1));
        var cache = new FreeRedisCacheManager(_client, configuration);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);
        var leaseKey = CacheKeyFormat.GetLeaseKey(configuration, key);

        try
        {
            await cache.SetAsync(key, 1);
            _client.Set(leaseKey, "lease");
            await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => cache.RemoveAllAsync(new[] { key }));
            Assert.Contains("UTF-8", error.Message, StringComparison.OrdinalIgnoreCase);

            var marker = "delete-budget-limit-observed-" + suffix;
            await _client.CallAsync(new CommandPacket("PING", marker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(marker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var namespaceMarker = GetNamespaceMarker(configuration);
            Assert.Equal(0, monitor.Count(line =>
                line.Contains("\"del\"", StringComparison.OrdinalIgnoreCase) &&
                line.Contains(namespaceMarker, StringComparison.Ordinal)));
            Assert.True(_client.Exists(physicalKey));
            Assert.True(_client.Exists(leaseKey));
        }
        finally
        {
            _client.Del(physicalKey, leaseKey);
        }
    }

    /// <summary>
    /// 写入逻辑键并创建对应的租约键。
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
    /// 计算物理缓存键及其租约键占用的 UTF-8 字节数。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>两个键的 UTF-8 字节数之和。</returns>
    private static long GetPairBytes(string physicalKey) =>
        Encoding.UTF8.GetByteCount(physicalKey) * 2L + Encoding.UTF8.GetByteCount(":lease");

    /// <summary>
    /// 生成用于筛选指定缓存命名空间物理键的前缀。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>包含命名空间和 Provider 名称的物理键前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";
}
