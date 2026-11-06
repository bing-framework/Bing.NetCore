using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 在纯内存配置下测量已命中键的同步完成路径，避免把 Redis 与事件监听成本混入单次分配。
/// </summary>
/// <param name="output">用于保存测量证据的测试输出。</param>
public class FusionCacheHotPathPerformanceTest(ITestOutputHelper output)
{
    private const int WarmupOperations = 10_000;
    private const int BatchCount = 5;
    private const int OperationsPerBatch = 20_000;

    /// <summary>
    /// 分别测量两个公开读取入口；每个入口预热后采集五批，校验值正确且没有执行回源。
    /// </summary>
    /// <remarks>
    /// 本测试保留测量原始值，不设置机器相关延迟阈值。线程分配量只适用于本测试中同步完成的 L1 路径。
    /// </remarks>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task MeasureTryGetAndGetOrCreateL1Hits()
    {
        using var services = new ServiceCollection().AddBingFusionCache("hot-path", options =>
        {
            options.Namespace = "performance:pure-l1";
            options.UseAsDefault = true;
        }).BuildServiceProvider();
        var cache = services.GetRequiredService<IHybridCache>();
        await cache.SetAsync("hot", 42);
        var factoryCalls = 0;
        Task<int> UnexpectedFactory(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("L1 命中探针意外执行回源工厂。");
        }

        // 委托在测量前创建，结果校验和计时使用同一条预热路径。
        Func<CancellationToken, Task<int>> factory = UnexpectedFactory;
        var results = new[]
        {
            Measure("pure-l1-tryget", () => cache.TryGetAsync<int>("hot"),
                static value => value.Hit && value.Value == 42),
            Measure("pure-l1-getorcreate", () => cache.GetOrCreateAsync("hot", factory),
                static value => value == 42)
        };
        Assert.Equal(0, factoryCalls);
        var json = JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            StopwatchFrequency = Stopwatch.Frequency,
            WarmupOperations,
            OperationsPerBatch,
            FactoryCalls = factoryCalls,
            AllocationScope = "Current managed thread; synchronously completed L1 hits only",
            Results = results
        }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        var root = Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_RESULTS");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, $"hotpath-net{Environment.Version.Major}.json"),
            json, new UTF8Encoding(false));
    }

    /// <summary>
    /// 预热并采集独立批次；采样数组、排序、汇总及 JSON 输出不计入线程分配。
    /// </summary>
    private static HotPathEvidence Measure<T>(string scenario, Func<Task<T>> operation, Func<T, bool> validate)
    {
        for (var i = 0; i < WarmupOperations; i++) ReadCompleted(operation, validate);
        var batches = new HotPathBatch[BatchCount];
        var allLatencies = new double[BatchCount * OperationsPerBatch];
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var latencies = new double[OperationsPerBatch];
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < latencies.Length; i++) latencies[i] = ReadCompleted(operation, validate);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            Array.Copy(latencies, 0, allLatencies, batch * OperationsPerBatch, OperationsPerBatch);
            Array.Sort(latencies);
            batches[batch] = new HotPathBatch(batch + 1, latencies.Length, elapsed, latencies.Average(),
                Percentile(latencies, 0.50), Percentile(latencies, 0.95), Percentile(latencies, 0.99),
                allocated, (double)allocated / latencies.Length);
        }

        Array.Sort(allLatencies);
        var totalAllocated = batches.Sum(batch => batch.AllocatedBytes);
        return new HotPathEvidence(scenario, allLatencies.Length, allLatencies.Average(),
            Percentile(allLatencies, 0.50), Percentile(allLatencies, 0.95), Percentile(allLatencies, 0.99),
            totalAllocated, (double)totalAllocated / allLatencies.Length, batches);
    }

    /// <summary>
    /// 验证任务同步完成后才读取结果，避免在线程分配探针中跨线程等待。
    /// </summary>
    private static double ReadCompleted<T>(Func<Task<T>> operation, Func<T, bool> validate)
    {
        var started = Stopwatch.GetTimestamp();
        var pending = operation();
        if (!pending.IsCompletedSuccessfully)
            throw new InvalidOperationException("纯 L1 探针要求操作同步成功完成。");
        var value = pending.GetAwaiter().GetResult();
        var elapsedUs = (Stopwatch.GetTimestamp() - started) * (1_000_000d / Stopwatch.Frequency);
        if (!validate(value)) throw new InvalidOperationException("纯 L1 探针读取到了错误的缓存值。");
        return elapsedUs;
    }

    /// <summary>
    /// 按 floor((样本数 - 1) × percentile) 读取有序样本的百分位。
    /// </summary>
    private static double Percentile(double[] sorted, double percentile) =>
        sorted[(int)Math.Floor((sorted.Length - 1) * percentile)];

    /// <summary>
    /// 单批测量数据；包含计时及值校验的批次总时长与单次操作延迟分别保留。
    /// </summary>
    private sealed record HotPathBatch(int Batch, int Samples, double ElapsedMs, double MeanUs,
        double P50Us, double P95Us, double P99Us, long AllocatedBytes, double BytesPerOperation);

    /// <summary>
    /// 一个公开入口的合并数据及全部批次，便于识别分层 JIT 与系统调度噪声。
    /// </summary>
    private sealed record HotPathEvidence(string Scenario, int Samples, double MeanUs,
        double P50Us, double P95Us, double P99Us, long AllocatedBytes, double BytesPerOperation,
        HotPathBatch[] Batches);
}
