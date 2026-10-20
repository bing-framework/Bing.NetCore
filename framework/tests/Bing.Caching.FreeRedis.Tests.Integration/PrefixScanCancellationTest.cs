using Bing.Caching;
using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 验证 FreeRedis 前缀流取消后不会继续派发 SCAN 命令。
/// </summary>
public sealed class PrefixScanCancellationTest
{
    /// <summary>
    /// 取消前缀流式读取后不再派发后续 SCAN 命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetByPrefixStream_CancellationStopsFurtherScanCommands()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = new RedisClient(connection)
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        var configuration = new CacheConfiguration(
            "freeredis-prefix-scan-cancel-" + Guid.NewGuid().ToString("N"),
            "scan-cancel",
            batchSize: 2);
        var cache = new FreeRedisCacheManager(client, configuration);
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
            await client.CallAsync(new CommandPacket("PING", firstPingMarker));
            await monitor.ReadUntilAsync(
                line => IsPing(line, firstPingMarker), TimeSpan.FromSeconds(5));
            var scanCountBeforeCancellation = monitor.Count(isNamespaceScan);
            Assert.True(scanCountBeforeCancellation >= 1);

            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "prefix-scan-cancel-" + Guid.NewGuid().ToString("N");
            await client.CallAsync(new CommandPacket("PING", pingMarker));
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
    /// 判断 MONITOR 行是否包含目标命名空间的 SCAN 命令。
    /// </summary>
    /// <param name="line">Redis MONITOR 输出行。</param>
    /// <param name="namespaceMarker">目标缓存命名空间的物理键前缀。</param>
    /// <returns>行中同时包含 SCAN 命令和命名空间前缀时为 <see langword="true"/>。</returns>
    private static bool IsNamespaceScan(string line, string namespaceMarker) =>
        line.Contains("\"scan\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(namespaceMarker, StringComparison.Ordinal);

    /// <summary>
    /// 判断 MONITOR 行是否包含指定标记对应的 PING 命令。
    /// </summary>
    /// <param name="line">Redis MONITOR 输出行。</param>
    /// <param name="marker">测试标记。</param>
    /// <returns>行中同时包含 PING 命令和指定标记时为 <see langword="true"/>。</returns>
    private static bool IsPing(string line, string marker) =>
        line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(marker, StringComparison.Ordinal);
}
