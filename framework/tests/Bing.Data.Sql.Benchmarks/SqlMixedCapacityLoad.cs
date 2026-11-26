using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>在固定窗口内调度读写操作，窗口结束后等待已开始的操作完成。</summary>
internal static class SqlMixedCapacityLoad
{
    internal const int LatencyBucketWidthMilliseconds = 1;
    internal const int LatencyHistogramMaxMilliseconds = 120_000;

    internal static bool IsWriteRequest(int request) => request % 10 == 9;

    internal static async Task<Result> RunAsync(TimeSpan window, int concurrency, int readPercent,
        int commandTimeoutSeconds, Func<int, bool, Task> execute, bool traceMeasurement = false)
    {
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        if (concurrency <= 0) throw new ArgumentOutOfRangeException(nameof(concurrency));
        if (readPercent != 90) throw new ArgumentOutOfRangeException(nameof(readPercent), "R02 currently fixes the workload at 90:10.");
        if (commandTimeoutSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
        ArgumentNullException.ThrowIfNull(execute);

        var histogram = new long[LatencyHistogramMaxMilliseconds + 1];
        var gate = new object();
        var stopwatch = new Stopwatch();
        var request = 0;
        var attempted = 0;
        var readAttempts = 0;
        var writeAttempts = 0;
        var successful = 0;
        var successfulWithinWindow = 0;
        var failed = 0;
        var failedReads = 0;
        var failedWrites = 0;
        var inFlight = 0;
        var peakInFlight = 0;
        var completedAfterWindow = 0;
        var histogramOverflow = 0;
        string firstFailureType = null;
        using var process = Process.GetCurrentProcess();
        var cpuStarted = process.TotalProcessorTime;
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var allocatedStarted = GC.GetTotalAllocatedBytes(precise: true);

        async Task Worker()
        {
            while (true)
            {
                int id;
                bool write;
                lock (gate)
                {
                    if (stopwatch.Elapsed >= window)
                        return;
                    id = request++;
                    write = IsWriteRequest(id);
                    attempted++;
                    if (write) writeAttempts++; else readAttempts++;
                }

                var active = Interlocked.Increment(ref inFlight);
                UpdatePeak(ref peakInFlight, active);
                var started = Stopwatch.GetTimestamp();
                var wasCompletedWithinWindow = false;
                try
                {
                    await execute(id, write).ConfigureAwait(false);
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    Interlocked.Increment(ref successful);
                    wasCompletedWithinWindow = stopwatch.Elapsed <= window;
                    if (wasCompletedWithinWindow)
                        Interlocked.Increment(ref successfulWithinWindow);

                    if (elapsed > LatencyHistogramMaxMilliseconds)
                        Interlocked.Increment(ref histogramOverflow);
                    else
                    {
                        var bucket = (int)Math.Clamp(Math.Ceiling(elapsed), 0, LatencyHistogramMaxMilliseconds);
                        Interlocked.Increment(ref histogram[bucket]);
                    }
                }
                catch (Exception error)
                {
                    Interlocked.Increment(ref failed);
                    if (write) Interlocked.Increment(ref failedWrites); else Interlocked.Increment(ref failedReads);
                    Interlocked.CompareExchange(ref firstFailureType, error.GetType().FullName, null);
                }
                finally
                {
                    if (stopwatch.Elapsed >= window)
                        Interlocked.Increment(ref completedAfterWindow);
                    Interlocked.Decrement(ref inFlight);
                }
            }
        }

        // 每个窗口只读取两次 UTC；外部 cgroup 样本据此对齐，不在请求热路径埋点。
        var startedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long stoppedAtUnixMilliseconds;
        if (traceMeasurement) SqlMixedCapacityEvents.Log.MeasurementStart();
        stopwatch.Start();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Task.Run(Worker))).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            if (traceMeasurement) SqlMixedCapacityEvents.Log.MeasurementStop();
            stoppedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        var summary = SummarizeLatencies(histogram, successful, histogramOverflow);
        var elapsedIncludingDrain = stopwatch.Elapsed.TotalMilliseconds;
        var cpuMilliseconds = (process.TotalProcessorTime - cpuStarted).TotalMilliseconds;
        return new Result(attempted, readAttempts, writeAttempts,
            attempted == 0 ? 0 : writeAttempts * 100d / attempted,
            successful, successfulWithinWindow, failed, failedReads, failedWrites, peakInFlight,
            window.TotalMilliseconds, elapsedIncludingDrain, completedAfterWindow,
            attempted / window.TotalSeconds,
            successfulWithinWindow / window.TotalSeconds,
            elapsedIncludingDrain <= 0 ? 0 : successful / (elapsedIncludingDrain / 1000d),
            commandTimeoutSeconds, cpuMilliseconds,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedStarted,
            GC.CollectionCount(0) - gc0, GC.CollectionCount(1) - gc1, GC.CollectionCount(2) - gc2,
            summary.P50Milliseconds, summary.P95Milliseconds, summary.P99Milliseconds,
            LatencyBucketWidthMilliseconds, LatencyHistogramMaxMilliseconds, histogramOverflow, firstFailureType,
            startedAtUnixMilliseconds, stoppedAtUnixMilliseconds);
    }

    /// <summary>返回固定 1ms 桶的 nearest-rank 分位数；落入溢出区间的分位数返回 null。</summary>
    internal static (double? P50Milliseconds, double? P95Milliseconds, double? P99Milliseconds) SummarizeLatencies(
        long[] histogram, int successful, int overflowCount)
    {
        if (successful <= 0) return (null, null, null);
        if (histogram.Length != LatencyHistogramMaxMilliseconds + 1)
            throw new ArgumentException("Unexpected fixed latency histogram length.", nameof(histogram));
        if (overflowCount < 0) throw new ArgumentOutOfRangeException(nameof(overflowCount));

        double? Percentile(double fraction)
        {
            var rank = (long)Math.Ceiling(successful * fraction);
            long accumulated = 0;
            for (var bucket = 0; bucket < histogram.Length; bucket++)
            {
                accumulated += histogram[bucket];
                if (accumulated >= rank) return bucket;
            }
            return rank <= accumulated + overflowCount ? null :
                throw new InvalidOperationException("Latency histogram does not contain all successful samples.");
        }

        return (Percentile(0.50), Percentile(0.95), Percentile(0.99));
    }

    private static void UpdatePeak(ref int peak, int value)
    {
        for (var current = Volatile.Read(ref peak); value > current; current = Volatile.Read(ref peak))
            if (Interlocked.CompareExchange(ref peak, value, current) == current) return;
    }

    internal sealed record Result(int Attempted, int ReadAttempts, int WriteAttempts, double ActualWritePercent,
        int Successful, int SuccessfulWithinWindow, int Failed, int FailedReads, int FailedWrites, int PeakInFlight,
        double WindowMilliseconds, double ElapsedIncludingDrainMilliseconds, int CompletedAfterWindow,
        double AdmissionRequestsPerSecond, double SuccessfulCompletionsWithinWindowPerSecond,
        double SuccessfulCompletionsIncludingDrainPerSecond, int CommandTimeoutSeconds,
        double ProcessCpuMilliseconds, long AllocatedBytes, int Gen0Collections, int Gen1Collections,
        int Gen2Collections, double? P50Milliseconds, double? P95Milliseconds, double? P99Milliseconds,
        int LatencyBucketWidthMilliseconds, int LatencyHistogramMaxMilliseconds, int LatencyHistogramOverflowCount,
        string FirstFailureType, long StartedAtUnixMilliseconds, long StoppedAtUnixMilliseconds);
}
