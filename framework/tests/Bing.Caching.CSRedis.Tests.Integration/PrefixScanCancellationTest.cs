using Bing.Caching;
using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 前缀流取消测试。
/// </summary>
/// <remarks>
/// 使用 Redis MONITOR 验证取消后不再派发下一次 SCAN。
/// </remarks>
public sealed class PrefixScanCancellationTest
{
    /// <summary>
    /// 验证取消前缀流后不再派发后续扫描命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetByPrefixStream_CancellationStopsFurtherScanCommands()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = new CSRedisClient(connection);
        var configuration = new CacheConfiguration(
            "csredis-prefix-scan-cancel-" + Guid.NewGuid().ToString("N"),
            "scan-cancel",
            batchSize: 2);
        var cache = new CSRedisCacheManager(client, configuration);
        var prefix = "scan-cancel-item:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 4).Select(index => prefix + index).ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));
        await using var monitor = await RedisCommandMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetByPrefixStreamAsync<int>(prefix, cancellation.Token).GetAsyncEnumerator();
        var namespaceMarker = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":";
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Func<string, bool> isNamespaceScan = line => IsNamespaceScan(line, namespaceMarker);
            await monitor.ReadUntilAsync(isNamespaceScan, TimeSpan.FromSeconds(5));
            var firstPingMarker = "prefix-scan-start-" + Guid.NewGuid().ToString("N");
            await PingMarkerAsync(client, firstPingMarker);
            await monitor.ReadUntilAsync(
                line => IsPing(line, firstPingMarker), TimeSpan.FromSeconds(5));
            var scanCountBeforeCancellation = monitor.Count(isNamespaceScan);
            Assert.True(scanCountBeforeCancellation >= 1);

            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "prefix-scan-cancel-" + Guid.NewGuid().ToString("N");
            await PingMarkerAsync(client, pingMarker);
            await monitor.ReadUntilAsync(
                line => IsPing(line, pingMarker), TimeSpan.FromSeconds(5));

            Assert.Equal(scanCountBeforeCancellation, monitor.Count(isNamespaceScan));
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 向 Redis 发送用于标记命令顺序的 PING。
    /// </summary>
    /// <param name="client">Redis 客户端。</param>
    /// <param name="marker">测试标记。</param>
    private static async Task PingMarkerAsync(CSRedisClient client, string marker)
    {
        // CSRedisClient.PingAsync(string) 的参数是路由键，不会作为 PING 消息发出。
        using var connection = await client.Nodes.First().Value.GetAsync();
        await connection.Value.CallAsync("PING", marker);
    }

    /// <summary>
    /// 判断 MONITOR 输出是否为当前命名空间的扫描命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="namespaceMarker">当前命名空间的物理键前缀。</param>
    /// <returns>输出包含扫描命令和指定命名空间时返回 <see langword="true"/>。</returns>
    private static bool IsNamespaceScan(string line, string namespaceMarker) =>
        line.Contains("\"scan\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(namespaceMarker, StringComparison.Ordinal);

    /// <summary>
    /// 判断 MONITOR 输出是否为指定标记的 PING 命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="marker">测试标记。</param>
    /// <returns>输出包含 PING 命令和指定标记时返回 <see langword="true"/>。</returns>
    private static bool IsPing(string line, string marker) =>
        line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(marker, StringComparison.Ordinal);
}
