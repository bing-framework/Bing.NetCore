using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量固定请求数的闭环并发负载，每个并发名额完成一次请求后才领取下一次。</summary>
internal static class SqlCapacityLoad
{
    internal static async Task<Result> RunAsync(int requests, int concurrency, Func<int, Task> execute)
    {
        var samples = new double[requests];
        var inFlight = 0;
        var peakInFlight = 0;
        string firstFailureType = null;
        using var process = Process.GetCurrentProcess();
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var cpu = process.TotalProcessorTime;
        var started = Stopwatch.GetTimestamp();

        await Parallel.ForAsync(0, requests, new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (request, _) =>
            {
                var active = Interlocked.Increment(ref inFlight);
                for (var peak = Volatile.Read(ref peakInFlight); active > peak; peak = Volatile.Read(ref peakInFlight))
                {
                    if (Interlocked.CompareExchange(ref peakInFlight, active, peak) == peak)
                        break;
                }
                var requestStarted = Stopwatch.GetTimestamp();
                try
                {
                    await execute(request);
                    samples[request] = Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds;
                }
                catch (Exception error)
                {
                    samples[request] = double.NaN;
                    Interlocked.CompareExchange(ref firstFailureType, error.GetType().FullName, null);
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            });

        var elapsed = Stopwatch.GetElapsedTime(started);
        var cpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        var gen0 = GC.CollectionCount(0) - gc0;
        var gen1 = GC.CollectionCount(1) - gc1;
        var gen2 = GC.CollectionCount(2) - gc2;
        var (successful, p50, p95, p99) = SummarizeLatencies(samples);
        return new Result(successful, requests - successful, peakInFlight, elapsed.TotalMilliseconds,
            successful / elapsed.TotalSeconds, p50, p95, p99, allocatedBytes, gen0, gen1, gen2,
            cpuMilliseconds, firstFailureType);
    }

    /// <summary>移除失败标记，再用成功样本数计算 nearest-rank 百分位。</summary>
    internal static (int Successful, double? P50, double? P95, double? P99) SummarizeLatencies(double[] samples)
    {
        var successful = 0;
        foreach (var sample in samples)
        {
            if (!double.IsNaN(sample))
                samples[successful++] = sample;
        }
        if (successful == 0)
            return (0, null, null, null);

        Array.Sort(samples, 0, successful);
        double Percentile(double fraction) => samples[(int)Math.Ceiling(successful * fraction) - 1];
        return (successful, Percentile(0.50), Percentile(0.95), Percentile(0.99));
    }

    internal sealed record Result(int Successful, int Failed, int PeakInFlight, double ElapsedMilliseconds,
        double SuccessfulRequestsPerSecond, double? P50Milliseconds, double? P95Milliseconds, double? P99Milliseconds,
        long AllocatedBytes, int Gen0Collections, int Gen1Collections, int Gen2Collections,
        double ProcessCpuMilliseconds, string FirstFailureType);
}
