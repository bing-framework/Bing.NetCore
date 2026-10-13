using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using System.Diagnostics;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 仅在隔离 Redis 集成环境和基准测试开关启用时运行性能探针。
/// </summary>
public sealed class CacheBenchmarkFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="CacheBenchmarkFactAttribute"/> 类型的实例。
    /// </summary>
    public CacheBenchmarkFactAttribute()
    {
        if (!IntegrationTestGate.IsProviderEnabled() ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_BENCHMARKS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需启用隔离 Redis 集成环境和 RUN_CACHE_BENCHMARKS=true。";
    }
}

/// <summary>
/// 采集 FreeRedis 批量读写和删除的性能数据。
/// </summary>
public class CachePerformanceProbeTest
{
    /// <summary>
    /// Redis 集成测试客户端。
    /// </summary>
    private readonly RedisClient _client;
    /// <summary>
    /// 保存代理输出流。
    /// </summary>
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    /// <summary>
    /// 初始化一个 <see cref="CachePerformanceProbeTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    /// <param name="output">测试输出辅助对象。</param>
    public CachePerformanceProbeTest(RedisClient client, Xunit.Abstractions.ITestOutputHelper output)
    {
        _client = client;
        _output = output;
    }

    /// <summary>
    /// 测量一千和一万项缓存数据的批量写入、读取和删除耗时。
    /// </summary>
    [CacheBenchmarkFact]
    public async Task Batch_OneAndTenThousand_CurrentCandidate()
    {
        foreach (var count in new[] { 1000, 10000 })
        {
            var cache = new FreeRedisCacheManager(_client,
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
