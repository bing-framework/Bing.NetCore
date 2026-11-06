using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 在同一缓存实例上对照原生接口与 Bing 门面的等价读取路径。
/// </summary>
public class FusionCacheNativeComparisonPerformanceTest(ITestOutputHelper output)
{
    private const int Warmup = 5_000;
    private const int BatchCount = 3;
    private const int OperationsPerBatch = 10_000;

    /// <summary>
    /// 仅测量同步完成的 L1 路径；原生接口使用预先编码的相同键与标签。
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task CompareL1HitsMissesAndTaggedHits()
    {
        using var services = new ServiceCollection().AddBingFusionCache("compare", o =>
        {
            o.Namespace = "performance:compare-l1";
            o.UseAsDefault = true;
        }).BuildServiceProvider();
        var cache = services.GetRequiredService<IHybridCache>();
        var native = services.GetRequiredService<IFusionCacheProvider>().GetCache("compare");
        var options = native.DefaultEntryOptions.Duplicate();
        options.AllowStaleOnReadOnly = false;
        string[] tags = ["group"];
        string[] nativeTags = ["t:group"];
        await cache.SetAsync("hot", 42);
        await cache.SetAsync("tagged", 42, tags: tags);
        Func<CancellationToken, Task<int>> factory = _ => throw new InvalidOperationException("意外回源");
        Func<FusionCacheFactoryExecutionContext<int>, CancellationToken, Task<int>> nativeFactory =
            (_, _) => throw new InvalidOperationException("意外回源");

        var results = new[]
        {
            MeasureCompleted("native-tryget-hit", () => native.TryGetAsync<int>("d:hot", options),
                static x => x.HasValue && x.Value == 42),
            MeasureCompleted<CacheEntry<int>>("bing-tryget-hit", () => new(cache.TryGetAsync<int>("hot")),
                static x => x.Hit && x.Value == 42),
            MeasureCompleted("native-tryget-miss", () => native.TryGetAsync<int>("d:missing", options),
                static x => !x.HasValue),
            MeasureCompleted<CacheEntry<int>>("bing-tryget-miss", () => new(cache.TryGetAsync<int>("missing")),
                static x => !x.Hit),
            MeasureCompleted("native-getorcreate-hit", () => native.GetOrSetAsync<int>("d:hot", nativeFactory, options: options),
                static x => x == 42),
            MeasureCompleted<int>("bing-getorcreate-hit", () => new(cache.GetOrCreateAsync("hot", factory)),
                static x => x == 42),
            MeasureCompleted("native-getorcreate-tagged-hit", () => native.GetOrSetAsync<int>("d:tagged", nativeFactory, options: options, tags: nativeTags),
                static x => x == 42),
            MeasureCompleted<int>("bing-getorcreate-tagged-hit", () => new(cache.GetOrCreateAsync("tagged", factory, tags: tags)),
                static x => x == 42)
        };
        Save("l1", "当前线程分配；仅统计同步完成的 L1 路径", results);
    }

    /// <summary>
    /// 跳过 L1，顺序读取同一 Redis 键；异步路径不使用当前线程分配统计。
    /// </summary>
    [RedisFact]
    [Trait("Category", "Performance")]
    public async Task CompareRedisL2HitsAndMisses()
    {
        using var services = RedisTestHost.Create(RedisTestSupport.NewNamespace(), "compare", onlyL2: true);
        var cache = services.GetRequiredService<IHybridCache>();
        var native = services.GetRequiredService<IFusionCacheProvider>().GetCache("compare");
        var options = native.DefaultEntryOptions.Duplicate();
        options.AllowStaleOnReadOnly = false;
        await cache.SetAsync("hot", 42);
        var results = new[]
        {
            await MeasureAsync("native-l2-tryget-hit", () => native.TryGetAsync<int>("d:hot", options),
                static x => x.HasValue && x.Value == 42),
            await MeasureAsync<CacheEntry<int>>("bing-l2-tryget-hit", () => new(cache.TryGetAsync<int>("hot")),
                static x => x.Hit && x.Value == 42),
            await MeasureAsync("native-l2-tryget-miss", () => native.TryGetAsync<int>("d:missing", options),
                static x => !x.HasValue),
            await MeasureAsync<CacheEntry<int>>("bing-l2-tryget-miss", () => new(cache.TryGetAsync<int>("missing")),
                static x => !x.Hit)
        };
        Save("l2", "异步 L2 路径不报告线程分配", results);
    }

    private static ComparisonResult MeasureCompleted<T>(string scenario, Func<ValueTask<T>> operation, Func<T, bool> validate)
    {
        for (var i = 0; i < Warmup; i++) ReadCompleted(operation, validate);
        var timings = new double[BatchCount * OperationsPerBatch];
        var startBytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < timings.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            ReadCompleted(operation, validate);
            timings[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        return Summarize(scenario, timings, (double)allocated / timings.Length);
    }

    private static void ReadCompleted<T>(Func<ValueTask<T>> operation, Func<T, bool> validate)
    {
        var pending = operation();
        if (!pending.IsCompletedSuccessfully || !validate(pending.GetAwaiter().GetResult()))
            throw new InvalidOperationException("L1 测量要求同步完成并返回预期值。");
    }

    private static async Task<ComparisonResult> MeasureAsync<T>(string scenario, Func<ValueTask<T>> operation, Func<T, bool> validate)
    {
        for (var i = 0; i < 20; i++)
            if (!validate(await operation())) throw new InvalidOperationException("L2 预热返回了错误的值。");
        var timings = new double[300];
        for (var i = 0; i < timings.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var result = await operation();
            timings[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            if (!validate(result)) throw new InvalidOperationException("L2 测量返回了错误的值。");
        }
        return Summarize(scenario, timings, null);
    }

    private static ComparisonResult Summarize(string scenario, double[] timings, double? bytesPerOperation)
    {
        Array.Sort(timings);
        double Percentile(double p) => timings[(int)Math.Floor((timings.Length - 1) * p)];
        return new(scenario, timings.Length, timings.Average(), Percentile(0.50), Percentile(0.95),
            Percentile(0.99), bytesPerOperation);
    }

    private void Save(string layer, string allocationScope, ComparisonResult[] results)
    {
        var json = JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            StopwatchFrequency = Stopwatch.Frequency,
            AllocationScope = allocationScope,
            Notes = "相同原生实例、键与标签；原生调用预先完成编码，门面包含编码及契约保护成本。",
            Results = results
        }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        var root = Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_RESULTS");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, $"native-comparison-{layer}-net{Environment.Version.Major}.json"),
            json, new UTF8Encoding(false));
    }

    private sealed record ComparisonResult(string Scenario, int Samples, double MeanUs,
        double P50Us, double P95Us, double P99Us, double? BytesPerOperation);
}
