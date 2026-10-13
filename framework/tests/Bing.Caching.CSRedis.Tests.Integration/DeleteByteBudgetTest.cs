using System.Text;
using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 删除命令 UTF-8 字节预算测试。
/// </summary>
/// <remarks>
/// 使用 Redis MONITOR 验证删除命令按配置的 UTF-8 字节预算切分。
/// </remarks>
public sealed class DeleteByteBudgetTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于写入和校验物理数据键与租约键并发送监控标记的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="DeleteByteBudgetTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供删除预算测试创建缓存管理器、写入租约键并校验删除结果的 Redis 客户端。</param>
    public DeleteByteBudgetTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证 RemoveAllAsync 按 UTF-8 字节预算拆分删除。
    /// </summary>
    /// <remarks>
    /// 长键应拆分为符合命令预算的删除请求，并保留兄弟命名空间的哨兵键。
    /// </remarks>
    [IntegrationFact]
    public async Task RemoveAllAsync_SplitsLongUtf8KeysBeforeCommandBudgetAndPreservesSibling()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var namespaceName = "delete-budget-" + suffix;
        var providerName = "csredis";
        var shape = new CacheConfiguration(namespaceName, providerName, batchSize: 16);
        var keys = Enumerable.Range(0, 4)
            .Select(index => "订单-😀-" + suffix + "-" + index.ToString("D2"))
            .ToArray();
        var pairBytes = GetPairBytes(CacheKeyFormat.GetPhysicalKey(shape, keys[0]));
        var configuration = new CacheConfiguration(namespaceName, providerName,
            batchSize: 16, maxConcurrency: 2,
            resources: new CacheResourceOptions(maxBatchCommandBytes: pairBytes * 2 - 1));
        var cache = new CSRedisCacheManager(_client, configuration);
        var sibling = new CSRedisCacheManager(_client,
            new CacheConfiguration(namespaceName + "-sibling", providerName));

        try
        {
            await SeedAsync(cache, configuration, keys);
            sibling.Set("sentinel", 42);

            await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
            await cache.RemoveAllAsync(keys);

            var marker = "delete-budget-observed-" + suffix;
            await PingMarkerAsync(marker);
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
    /// 验证超出单键命令预算时提前拒绝删除。
    /// </summary>
    /// <remarks>
    /// 单个键超过预算时 RemoveAllAsync 应在发送 DEL 前失败，并保留数据键和租约键。
    /// </remarks>
    [IntegrationFact]
    public async Task RemoveAllAsync_RejectsSingleKeyOverCommandBudgetBeforeDispatch()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var namespaceName = "delete-budget-limit-" + suffix;
        var providerName = "csredis";
        var shape = new CacheConfiguration(namespaceName, providerName);
        var key = "超限-😀-" + suffix;
        var pairBytes = GetPairBytes(CacheKeyFormat.GetPhysicalKey(shape, key));
        var configuration = new CacheConfiguration(namespaceName, providerName,
            resources: new CacheResourceOptions(maxBatchCommandBytes: pairBytes - 1));
        var cache = new CSRedisCacheManager(_client, configuration);
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
            await PingMarkerAsync(marker);
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
    /// 写入删除预算测试所需的数据键和租约键。
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
    /// 发送用于界定 MONITOR 观察范围的 PING 标记。
    /// </summary>
    /// <param name="marker">写入 PING 消息的测试标记。</param>
    private async Task PingMarkerAsync(string marker)
    {
        using var connection = await _client.Nodes.First().Value.GetAsync();
        await connection.Value.CallAsync("PING", marker);
    }

    /// <summary>
    /// 计算数据键和租约键的 UTF-8 字节总数。
    /// </summary>
    /// <remarks>
    /// 租约键按在数据键后追加“:lease”计算。
    /// </remarks>
    /// <param name="physicalKey">数据键的物理名称。</param>
    /// <returns>数据键和追加“:lease”后租约键合计占用的 UTF-8 字节数。</returns>
    private static long GetPairBytes(string physicalKey) =>
        Encoding.UTF8.GetByteCount(physicalKey) * 2L + Encoding.UTF8.GetByteCount(":lease");

    /// <summary>
    /// 生成缓存命名空间的 MONITOR 识别前缀。
    /// </summary>
    /// <param name="configuration">包含命名空间和 Provider 名称的缓存配置。</param>
    /// <returns>按物理命名空间格式编码的前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";
}
