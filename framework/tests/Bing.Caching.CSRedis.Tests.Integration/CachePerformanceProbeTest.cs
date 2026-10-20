using Bing.Caching.CSRedis;
using Bing.Test.Shared;
using CSRedis;
using System.Diagnostics;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 缓存性能探测测试特性。
/// </summary>
/// <remarks>
/// 仅在启用 Redis 集成环境且设置 RUN_CACHE_BENCHMARKS=true 时运行测试。
/// </remarks>
public sealed class CacheBenchmarkFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="CacheBenchmarkFactAttribute"/> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 根据集成环境和 RUN_CACHE_BENCHMARKS 环境变量决定是否跳过测试。
    /// </remarks>
    public CacheBenchmarkFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_BENCHMARKS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需启用隔离 Redis 集成环境和 RUN_CACHE_BENCHMARKS=true。";
    }
}

/// <summary>
/// CSRedis 缓存批量操作性能测试。
/// </summary>
public class CachePerformanceProbeTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建待测缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;
    /// <summary>
    /// 保存 xUnit 输出助手，用于记录各批量操作的耗时和内存增量。
    /// </summary>
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    /// <summary>
    /// 初始化一个 <see cref="CachePerformanceProbeTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供性能测试创建批量操作目标缓存管理器的 Redis 客户端。</param>
    /// <param name="output">用于输出性能测量结果的 xUnit 输出助手。</param>
    public CachePerformanceProbeTest(CSRedisClient client, Xunit.Abstractions.ITestOutputHelper output)
    {
        _client = client;
        _output = output;
    }

    /// <summary>
    /// 测量缓存批量操作的耗时和内存增量。
    /// </summary>
    /// <remarks>
    /// 分别处理 1000 和 10000 个键，并覆盖批量写入、读取和删除操作。
    /// </remarks>
    [CacheBenchmarkFact]
    public async Task Batch_OneAndTenThousand_CurrentCandidate()
    {
        foreach (var count in new[] { 1000, 10000 })
        {
            var cache = new CSRedisCacheManager(_client,
                new CacheConfiguration("cache-probe-" + Guid.NewGuid().ToString("N"), "redis"));
            var values = Enumerable.Range(0, count).ToDictionary(index => "item:" + index, index => index);
            var keys = values.Keys.ToArray();
            var watch = Stopwatch.StartNew();
            var before = GC.GetTotalMemory(true);
            await cache.SetAllAsync(values);
            var writeMs = watch.ElapsedMilliseconds;
            watch.Restart();
            var read = await cache.GetAllAsync<int>(keys);
            var readMs = watch.ElapsedMilliseconds;
            watch.Restart();
            await cache.RemoveAllAsync(keys);
            var removeMs = watch.ElapsedMilliseconds;
            var memoryDelta = GC.GetTotalMemory(true) - before;
            Assert.Equal(count, read.Count);
            Assert.Equal(count - 1, read[count - 1]);
            _output.WriteLine($"count={count} writeMs={writeMs} readMs={readMs} removeMs={removeMs} " +
                $"memoryDeltaBytes={memoryDelta} maxConcurrency=8");
        }
    }
}
