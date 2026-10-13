using Bing.Caching;
using Bing.Test.Shared;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// EasyCaching Redis 扫描取消测试门控。
/// </summary>
/// <remarks>
/// 仅在启用集成测试且数据端点为本地专用 52800 Redis 时运行。
/// </remarks>
public sealed class EasyRedisScan52800FactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyRedisScan52800FactAttribute"/> 类型的实例。
    /// </summary>
    public EasyRedisScan52800FactAttribute()
    {
        var connection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !TryGetPort(connection, out var port) || port != 52800)
            Skip = "需要 RUN_INTEGRATION_TESTS=true 且 BING_TEST_EASYCACHING_REDIS 指向专用的 52800 Redis。";
    }

    /// <summary>
    /// 从 Redis 连接字符串中读取端口号。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="port">解析出的 Redis 端口号。</param>
    /// <returns>返回连接字符串是否包含可解析的端口。</returns>
    private static bool TryGetPort(string connection, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(connection))
            return false;
        var endpoint = connection.Split(',', 2)[0].Trim();
        if (endpoint.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
            endpoint = endpoint.Substring("redis://".Length);
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = endpoint.IndexOf(']');
            return closingBracket >= 0 &&
                int.TryParse(endpoint.Substring(closingBracket + 1).TrimStart(':', ' '), out port);
        }
        var separator = endpoint.LastIndexOf(':');
        return separator > 0 && int.TryParse(endpoint.Substring(separator + 1), out port);
    }
}

/// <summary>
/// EasyCaching 前缀流取消测试。
/// </summary>
/// <remarks>
/// 使用 Redis MONITOR 验证 Redis 和 Hybrid 前缀流取消后不再派发下一次 SCAN。
/// </remarks>
public sealed class PrefixScanCancellationTest
{
    /// <summary>
    /// Redis 前缀流取消后不再派发下一条 SCAN 命令。
    /// </summary>
    [EasyRedisScan52800Fact]
    public async Task Redis_GetByPrefixStream_CancellationStopsFurtherScanCommands()
    {
        var connection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var endpoint = ParseEndpoint(connection);
        var configuration = new CacheConfiguration(
            "easy-redis-prefix-scan-cancel-" + Guid.NewGuid().ToString("N"),
            "scan-cancel",
            batchSize: 2);
        using var container = BuildRedis(configuration, endpoint);
        var cache = container.GetRequiredService<IRedisCache>();
        var provider = container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName);
        var database = Assert.IsAssignableFrom<IDatabase>(provider.Database);
        await AssertScanStopsAsync(cache, database, configuration, connection);
    }

    /// <summary>
    /// Hybrid 前缀流取消后不再派发下一条 SCAN 命令。
    /// </summary>
    [EasyHybridPrefix52800Fact]
    public async Task Hybrid_GetByPrefixStream_CancellationStopsFurtherScanCommands()
    {
        var connection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var endpoint = ParseEndpoint(connection);
        var busEndpoint = ParseEndpoint(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS"));
        var configuration = new CacheConfiguration(
            "easy-hybrid-prefix-scan-cancel-" + Guid.NewGuid().ToString("N"),
            "scan-cancel",
            batchSize: 2);
        using var container = BuildHybrid(configuration, endpoint, busEndpoint);
        var cache = container.GetRequiredService<IRedisCache>();
        var provider = container.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName + ":distributed");
        var database = Assert.IsAssignableFrom<IDatabase>(provider.Database);
        await AssertScanStopsAsync(cache, database, configuration, connection);
    }

    /// <summary>
    /// 验证前缀流取消后扫描命令数量保持不变。
    /// </summary>
    /// <param name="cache">缓存实例。</param>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="connection">Redis 连接。</param>
    private static async Task AssertScanStopsAsync(IRedisCache cache, IDatabase database,
        CacheConfiguration configuration, string connection)
    {
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
            await database.ExecuteAsync("PING", firstPingMarker);
            await monitor.ReadUntilAsync(
                line => IsPing(line, firstPingMarker), TimeSpan.FromSeconds(5));
            var scanCountBeforeCancellation = monitor.Count(isNamespaceScan);
            Assert.True(scanCountBeforeCancellation >= 1);

            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "prefix-scan-cancel-" + Guid.NewGuid().ToString("N");
            await database.ExecuteAsync("PING", pingMarker);
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
    /// 判断 MONITOR 行是否属于当前命名空间的 SCAN 命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="namespaceMarker">当前缓存命名空间标记。</param>
    /// <returns>如果 MONITOR 行包含当前命名空间的 SCAN 命令，则返回 true。</returns>
    private static bool IsNamespaceScan(string line, string namespaceMarker) =>
        line.Contains("\"scan\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(namespaceMarker, StringComparison.Ordinal);

    /// <summary>
    /// 判断 MONITOR 行是否为指定标记对应的 PING 命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="marker">测试标记。</param>
    /// <returns>如果 MONITOR 行包含指定标记的 PING 命令，则返回 true。</returns>
    private static bool IsPing(string line, string marker) =>
        line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(marker, StringComparison.Ordinal);

    /// <summary>
    /// 创建使用 Redis Provider 的扫描取消测试容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回已注册 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildRedis(CacheConfiguration configuration,
        (string Host, int Port) endpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port)));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用 Hybrid Provider 的扫描取消测试容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="dataEndpoint">Redis 端点。</param>
    /// <param name="busEndpoint">Redis 端点。</param>
    /// <returns>返回已注册 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration,
        (string Host, int Port) dataEndpoint, (string Host, int Port) busEndpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint(dataEndpoint.Host, dataEndpoint.Port)),
            bus => bus.ConfigurationOptions = new ConfigurationOptions
            {
                EndPoints = { { busEndpoint.Host, busEndpoint.Port } },
                AbortOnConnectFail = false,
                SyncTimeout = 5000,
                AsyncTimeout = 5000
            });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 解析测试 Redis 连接字符串中的端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 测试连接不能为空。");
        var endpoint = connection.Split(',', 2)[0].Trim();
        if (endpoint.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
            endpoint = endpoint.Substring("redis://".Length);
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = endpoint.IndexOf(']');
            if (closingBracket < 0)
                throw new FormatException("Redis IPv6 地址格式无效。");
            var host = endpoint.Substring(1, closingBracket - 1);
            var suffix = endpoint.Substring(closingBracket + 1);
            var port = string.IsNullOrEmpty(suffix) ? 6379 :
                int.Parse(suffix.TrimStart(':', ' '));
            return (host, port);
        }
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint.Substring(separator + 1), out var parsedPort))
            return (endpoint, 6379);
        return (endpoint.Substring(0, separator), parsedPort);
    }
}
