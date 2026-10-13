using Bing.Caching;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis SCAN 页预算测试。
/// </summary>
/// <remarks>
/// 使用真实 Redis MONITOR 验证原生 SCAN 页预算在各扫描入口生效。
/// </remarks>
public sealed class ScanPageBudgetTest
{
    /// <summary>
    /// 扫描预算测试的预置键数量。
    /// </summary>
    private const int SeedCount = 128;
    /// <summary>
    /// 保存由测试夹具提供、用于创建缓存管理器并发送监控标记的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="ScanPageBudgetTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供扫描页预算测试创建缓存管理器并发送监控标记的 Redis 客户端。</param>
    public ScanPageBudgetTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证同步前缀读取的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task GetByPrefix_StopsBeforeSecondScanPage() => VerifyAsync(
        "get-sync",
        (cache, prefix) =>
        {
            cache.GetByPrefix<int>(prefix);
            return Task.CompletedTask;
        });

    /// <summary>
    /// 验证异步前缀读取的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task GetByPrefixAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "get-async",
        async (cache, prefix) => await cache.GetByPrefixAsync<int>(prefix));

    /// <summary>
    /// 验证流式前缀读取的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task GetByPrefixStream_StopsBeforeSecondScanPage() => VerifyAsync(
        "get-stream",
        async (cache, prefix) =>
        {
            await foreach (var _ in cache.GetByPrefixStreamAsync<int>(prefix))
            {
            }
        });

    /// <summary>
    /// 验证同步前缀删除的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task RemoveByPrefix_StopsBeforeSecondScanPage() => VerifyAsync(
        "remove-sync",
        (cache, prefix) =>
        {
            cache.RemoveByPrefix(prefix);
            return Task.CompletedTask;
        });

    /// <summary>
    /// 验证异步前缀删除的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task RemoveByPrefixAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "remove-async",
        async (cache, prefix) => await cache.RemoveByPrefixAsync(prefix));

    /// <summary>
    /// 验证同步清理的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task Clear_StopsBeforeSecondScanPage() => VerifyAsync(
        "clear-sync",
        (cache, _) =>
        {
            cache.Clear();
            return Task.CompletedTask;
        });

    /// <summary>
    /// 验证异步清理的 SCAN 页预算。
    /// </summary>
    /// <remarks>
    /// 达到单页预算后应停止，不发起第二页扫描。
    /// </remarks>
    [Fact]
    public Task ClearAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "clear-async",
        async (cache, _) => await cache.ClearAsync());

    /// <summary>
    /// 复用指定扫描入口执行页预算断言。
    /// </summary>
    /// <remarks>
    /// 操作应只消耗一个 SCAN 页，并由 Redis MONITOR 记录验证实际请求数量。
    /// </remarks>
    /// <param name="operationName">用于生成测试命名空间的操作名称。</param>
    /// <param name="operation">接收缓存管理器和逻辑前缀并执行待测扫描或清理入口的异步操作。</param>
    private async Task VerifyAsync(string operationName,
        Func<CSRedisCacheManager, string, Task> operation)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration(
            "csredis-scan-pages-" + operationName + "-" + suffix,
            "scan-pages",
            batchSize: 1,
            resources: new CacheResourceOptions(maxScanPages: 1));
        var cache = new CSRedisCacheManager(_client, configuration);
        var prefix = "页预算:" + suffix + ":";
        var keys = Enumerable.Range(0, SeedCount)
            .Select(index => prefix + index)
            .ToArray();

        try
        {
            await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
            await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);

            var error = await Assert.ThrowsAsync<CacheScanLimitExceededException>(
                () => operation(cache, prefix));
            Assert.Equal(CacheScanLimitKind.ScanPages, error.Kind);
            Assert.Equal(1, error.Limit);
            Assert.Equal(1, error.Consumed);

            var marker = "scan-pages-observed-" + suffix;
            await PingMarkerAsync(marker);
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(marker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var namespaceMarker = GetNamespaceMarker(configuration);
            var scanCount = monitor.ObservedLines.Count(line =>
                line.Contains("\"scan\"", StringComparison.OrdinalIgnoreCase) &&
                line.Contains(namespaceMarker, StringComparison.Ordinal));
            Assert.Equal(1, scanCount);
        }
        finally
        {
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 发送用于界定 MONITOR 扫描记录的 PING 标记。
    /// </summary>
    /// <param name="marker">写入 PING 消息的测试标记。</param>
    private async Task PingMarkerAsync(string marker)
    {
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
