using Bing.Caching;
using Bing.Test.Shared;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 仅在启用集成测试并配置隔离 EasyCaching Redis 环境时运行扫描预算用例。
/// </summary>
public sealed class EasyScanPageBudgetFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyScanPageBudgetFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyScanPageBudgetFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")))
            Skip = "需要隔离的 EasyCaching Redis 集成环境。";
    }
}

/// <summary>
/// 覆盖对应缓存集成测试场景。
/// </summary>
public sealed class ScanPageBudgetIntegrationTest
{
    /// <summary>
    /// 封装扫描测试操作。
    /// </summary>
    private enum ScanOperation
    {
        /// <summary>
        /// 执行同步前缀读取。
        /// </summary>
        GetByPrefix,
        /// <summary>
        /// 执行异步前缀读取。
        /// </summary>
        GetByPrefixAsync,
        /// <summary>
        /// 执行流式前缀读取。
        /// </summary>
        GetByPrefixStream,
        /// <summary>
        /// 执行同步前缀删除。
        /// </summary>
        RemoveByPrefix,
        /// <summary>
        /// 执行异步前缀删除。
        /// </summary>
        RemoveByPrefixAsync,
        /// <summary>
        /// 执行同步命名空间清理。
        /// </summary>
        Clear,
        /// <summary>
        /// 执行异步命名空间清理。
        /// </summary>
        ClearAsync
    }

    /// <summary>
    /// Redis 和 Hybrid 的前缀读取、删除与清理遵守原生扫描页预算。
    /// </summary>
    [EasyScanPageBudgetFact]
    public async Task RedisAndHybrid_RespectNativeScanPageBudget()
    {
        foreach (var hybrid in new[] { false, true })
        foreach (var operation in Enum.GetValues<ScanOperation>())
            await AssertOperationAsync(hybrid, operation);
    }

    /// <summary>
    /// 执行指定扫描操作并验证其资源预算错误。
    /// </summary>
    /// <param name="hybrid">是否使用 Hybrid Provider。</param>
    /// <param name="operation">待执行操作。</param>
    private static async Task AssertOperationAsync(bool hybrid, ScanOperation operation)
    {
        var configuration = new CacheConfiguration(
            "easy-scan-page-" + Guid.NewGuid().ToString("N"),
            hybrid ? "easy-scan-hybrid" : "easy-scan-redis",
            batchSize: 1,
            resources: new CacheResourceOptions(maxScanPages: 1));
        using var container = hybrid ? BuildHybrid(configuration) : BuildRedis(configuration);
        var cache = container.GetRequiredService<IRedisCache>();
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        var provider = factory.GetCachingProvider(hybrid
            ? configuration.ProviderName + ":distributed"
            : configuration.ProviderName);
        var database = Assert.IsAssignableFrom<IDatabase>(provider.Database);
        var prefix = "scan-page:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 128)
            .Select(index => prefix + index)
            .ToArray();
        var physicalKeys = keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray();
        var namespacePrefix = CacheKeyFormat.GetNamespacePattern(configuration).TrimEnd('*');

        await cache.SetAllAsync(keys.Select((key, index) =>
            new KeyValuePair<string, int>(key, index + 1)).ToDictionary(item => item.Key, item => item.Value));

        await using var monitor = await RedisCommandMonitor.ConnectAsync(
            Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS"));
        try
        {
            var error = await RunOperationAsync(cache, prefix, operation);
            var scanLine = await monitor.ReadUntilAsync(
                line => IsMatchingScan(line, namespacePrefix), TimeSpan.FromSeconds(10));

            var scanError = Assert.IsType<CacheScanLimitExceededException>(error);
            Assert.Equal(CacheScanLimitKind.ScanPages, scanError.Kind);
            Assert.Equal(1, scanError.Limit);
            Assert.Equal(1, scanError.Consumed);
            Assert.Equal(1, monitor.Count(line => IsMatchingScan(line, namespacePrefix)));
            Assert.Contains(namespacePrefix, scanLine, StringComparison.Ordinal);
        }
        finally
        {
            await database.KeyDeleteAsync(physicalKeys.Select(key => (RedisKey)key).ToArray());
        }
    }

    /// <summary>
    /// 执行指定的前缀读取、删除或清理操作。
    /// </summary>
    /// <returns>异步操作，完成后返回操作过程中捕获的异常；操作成功时返回 null。</returns>
    /// <param name="cache">缓存实例。</param>
    /// <param name="prefix">缓存键前缀。</param>
    /// <param name="operation">待执行操作。</param>
    private static async Task<Exception> RunOperationAsync(IRedisCache cache, string prefix,
        ScanOperation operation)
    {
        switch (operation)
        {
            case ScanOperation.GetByPrefix:
                return Record.Exception(() => cache.GetByPrefix<int>(prefix));
            case ScanOperation.GetByPrefixAsync:
                return await Record.ExceptionAsync(() => cache.GetByPrefixAsync<int>(prefix));
            case ScanOperation.GetByPrefixStream:
                return await Record.ExceptionAsync(async () =>
                {
                    await foreach (var _ in cache.GetByPrefixStreamAsync<int>(prefix))
                    {
                    }
                });
            case ScanOperation.RemoveByPrefix:
                return Record.Exception(() => cache.RemoveByPrefix(prefix));
            case ScanOperation.RemoveByPrefixAsync:
                return await Record.ExceptionAsync(() => cache.RemoveByPrefixAsync(prefix));
            case ScanOperation.Clear:
                return Record.Exception(() => cache.Clear());
            case ScanOperation.ClearAsync:
                return await Record.ExceptionAsync(() => cache.ClearAsync());
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    /// <summary>
    /// 判断 MONITOR 行是否为指定命名空间的 SCAN 命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="namespacePrefix">命名空间前缀。</param>
    /// <returns>如果 MONITOR 行包含指定命名空间的 SCAN 命令，则返回 true。</returns>
    private static bool IsMatchingScan(string line, string namespacePrefix) =>
        line.IndexOf("\"SCAN\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
        line.IndexOf(namespacePrefix, StringComparison.Ordinal) >= 0;

    /// <summary>
    /// 创建使用 Redis Provider 的扫描页预算测试容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildRedis(CacheConfiguration configuration)
    {
        var endpoint = ParseEndpoint();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port)));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用 Hybrid Provider 的扫描页预算测试容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var endpoint = ParseEndpoint();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCaching(configuration, configuration.ProviderName + ":distributed",
            redis => redis.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port)),
            bus => bus.ConfigurationOptions = new ConfigurationOptions
            {
                EndPoints = { { endpoint.Host, endpoint.Port } }
            });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 解析扫描预算测试使用的 Redis 端点。
    /// </summary>
    /// <returns>返回扫描预算测试使用的 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint()
    {
        var value = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var endpoint = value.Split(',')[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        return separator > 0 && int.TryParse(endpoint.Substring(separator + 1), out var port)
            ? (endpoint.Substring(0, separator), port)
            : (endpoint, 6379);
    }
}
