using Bing.Caching;
using Bing.Test.Shared;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// FreeRedis SCAN 页预算测试。
/// </summary>
/// <remarks>
/// 使用真实 Redis MONITOR 验证原生 SCAN 页预算在各扫描入口生效。
/// </remarks>
public sealed class ScanPageBudgetTest
{
    /// <summary>
    /// 保存初始节点数量。
    /// </summary>
    private const int SeedCount = 128;
    /// <summary>
    /// 用于配置扫描测试数据和监控 Redis 命令的客户端。
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个 <see cref="ScanPageBudgetTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public ScanPageBudgetTest(RedisClient client) => _client = client;

    /// <summary>
    /// 前缀读取达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task GetByPrefix_StopsBeforeSecondScanPage() => VerifyAsync(
        "get-sync",
        (cache, prefix) =>
        {
            cache.GetByPrefix<int>(prefix);
            return Task.CompletedTask;
        });

    /// <summary>
    /// 前缀读取达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task GetByPrefixAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "get-async",
        async (cache, prefix) => await cache.GetByPrefixAsync<int>(prefix));

    /// <summary>
    /// 前缀读取达到扫描页数上限后停止继续扫描。
    /// </summary>
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
    /// 前缀删除达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task RemoveByPrefix_StopsBeforeSecondScanPage() => VerifyAsync(
        "remove-sync",
        (cache, prefix) =>
        {
            cache.RemoveByPrefix(prefix);
            return Task.CompletedTask;
        });

    /// <summary>
    /// 前缀删除达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task RemoveByPrefixAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "remove-async",
        async (cache, prefix) => await cache.RemoveByPrefixAsync(prefix));

    /// <summary>
    /// 清理操作达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task Clear_StopsBeforeSecondScanPage() => VerifyAsync(
        "clear-sync",
        (cache, _) =>
        {
            cache.Clear();
            return Task.CompletedTask;
        });

    /// <summary>
    /// 清理操作达到扫描页数上限后停止继续扫描。
    /// </summary>
    [Fact]
    public Task ClearAsync_StopsBeforeSecondScanPage() => VerifyAsync(
        "clear-async",
        async (cache, _) => await cache.ClearAsync());

    /// <summary>
    /// 验证指定扫描操作达到页数上限后停止继续扫描。
    /// </summary>
    /// <param name="operationName">操作名称。</param>
    /// <param name="operation">待执行操作。</param>
    private async Task VerifyAsync(string operationName,
        Func<FreeRedisCacheManager, string, Task> operation)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        var suffix = Guid.NewGuid().ToString("N");
        var configuration = new CacheConfiguration(
            "freeredis-scan-pages-" + operationName + "-" + suffix,
            "scan-pages",
            batchSize: 1,
            resources: new CacheResourceOptions(maxScanPages: 1));
        var cache = new FreeRedisCacheManager(_client, configuration);
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
            await _client.CallAsync(new CommandPacket("PING", marker));
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
    /// 生成用于筛选当前命名空间物理键的前缀。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>包含命名空间和 Provider 名称的物理键前缀。</returns>
    private static string GetNamespaceMarker(CacheConfiguration configuration) =>
        "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
        Uri.EscapeDataString(configuration.ProviderName) + ":";
}
