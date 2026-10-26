using System.Diagnostics;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using Xunit.Abstractions;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 测量真实 Redis 场景下 L1、L2 和冷回源的延迟与资源占用。
/// </summary>
/// <param name="output">用于输出性能证据 JSON 的测试输出。</param>
public class FusionCacheResourceTest(ITestOutputHelper output)
{
    /// <summary>
    /// 依次测量 L1 命中、L2 命中、同键冷回源和不同键冷回源的资源指标。
    /// </summary>
    [RedisFact]
    public async Task MeasureL1L2AndColdFactoryWorkloads()
    {
        foreach (var scenario in new[] { "l1-hit", "l2-hit", "same-key-cold", "different-keys-cold" })
        {
            var name = "perf-" + scenario;
            var ns = RedisTestSupport.NewNamespace();
            using var observer = new AdmissionObserver(name);
            using var services = RedisTestHost.Create(ns, name, onlyL2: scenario == "l2-hit",
                configure: options => options.EnableSyncEventHandlersExecution = true);
            var cache = services.GetRequiredService<IHybridCache>();
            var native = services.GetRequiredService<IFusionCacheProvider>().GetCache(name);
            await cache.SetAsync("warm", 42);
            for (var warmup = 0; warmup < 20; warmup++) await cache.TryGetAsync<int>("warm");
            using var cacheEvents = new NativeCacheEventObserver(native);
            var cold = scenario.EndsWith("cold");
            var count = cold ? 64 : scenario == "l2-hit" ? 200 : 2000;
            var factoryCalls = 0;
            var timings = new double[count];
            using var measurement = PerformanceMeasurement.Start();
            async Task Execute(int index)
            {
                var key = !cold ? "warm" : scenario == "same-key-cold" ? "same" : "cold-" + index;
                var operationStarted = Stopwatch.GetTimestamp();
                var value = await cache.GetOrCreateAsync(key, async token =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    await Task.Delay(5, token);
                    return 42;
                });
                timings[index] = Stopwatch.GetElapsedTime(operationStarted).TotalMicroseconds;
                Assert.Equal(42, value);
            }
            if (cold)
                await Task.WhenAll(Enumerable.Range(0, count).Select(Execute)).WaitAsync(TimeSpan.FromSeconds(15));
            else
                for (var i = 0; i < count; i++) await Execute(i);
            var snapshot = measurement.Complete();
            Assert.Equal(cold ? scenario == "same-key-cold" ? 1 : count : 0, factoryCalls);
            Assert.Equal(cold ? factoryCalls : count, cold ? cacheEvents.MissCount : cacheEvents.HitCount);
            Assert.Equal(0, cacheEvents.FailSafeCount);
            Assert.InRange(observer.PeakActive, 0, 8);
            Assert.InRange(observer.PeakWaiting, 0, 128);
            PerformanceEvidence.Create(scenario, timings, factoryCalls, observer, snapshot, cacheEvents).Save(output);
        }
    }
}
