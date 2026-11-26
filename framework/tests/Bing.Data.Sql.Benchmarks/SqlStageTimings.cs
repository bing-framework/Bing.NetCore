using System;
using System.Diagnostics;
using System.Threading;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>诊断专用阶段；执行阶段包含连接获取，不能解释为纯连接池排队。</summary>
internal enum SqlRequestStage { Compose, Freeze, ReadExecute, WriteExecute }

/// <summary>只保留固定大小累加器，不保存逐请求样本。只在显式诊断模式启用。</summary>
internal sealed class SqlStageTimings
{
    private readonly long[] _counts = new long[4], _totals = new long[4], _maxima = new long[4];
    private const int BucketsPerMillisecond = 10, MaxMilliseconds = 100;
    // 四个阶段共约32KiB；超范围单独计数，热路径不保存请求对象。
    private readonly long[,] _histograms = new long[4, MaxMilliseconds * BucketsPerMillisecond + 1];
    private readonly long[] _overflows = new long[4];

    internal void Record(SqlRequestStage stage, long elapsedTicks)
    {
        var i = (int)stage;
        Interlocked.Add(ref _totals[i], elapsedTicks);
        Interlocked.Increment(ref _counts[i]);
        var milliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
        if (milliseconds > MaxMilliseconds) Interlocked.Increment(ref _overflows[i]);
        else Interlocked.Increment(ref _histograms[i, (int)Math.Ceiling(milliseconds * BucketsPerMillisecond)]);
        for (var current = Volatile.Read(ref _maxima[i]); elapsedTicks > current; current = Volatile.Read(ref _maxima[i]))
            if (Interlocked.CompareExchange(ref _maxima[i], elapsedTicks, current) == current) break;
    }

    /// <summary>调用方应在负载完成及收尾后读取，保证各字段属于同一个完整窗口。</summary>
    internal StageSummary[] Snapshot()
    {
        var names = new[] { "scope-and-compose", "read-freeze-plan", "read-execute-and-materialize", "write-execute" };
        var result = new StageSummary[4];
        for (var i = 0; i < result.Length; i++)
        {
            var count = Volatile.Read(ref _counts[i]);
            var total = Volatile.Read(ref _totals[i]);
            var max = Volatile.Read(ref _maxima[i]);
            result[i] = new StageSummary(names[i], count, total, max,
                count == 0 ? null : total * 1000d / Stopwatch.Frequency / count,
                count == 0 ? null : max * 1000d / Stopwatch.Frequency,
                Percentile(i, count, 0.50), Percentile(i, count, 0.95), Percentile(i, count, 0.99),
                1d / BucketsPerMillisecond, MaxMilliseconds, Volatile.Read(ref _overflows[i]));
        }
        return result;
    }

    private double? Percentile(int stage, long count, double fraction)
    {
        if (count == 0) return null;
        var rank = (long)Math.Ceiling(count * fraction);
        long accumulated = 0;
        for (var bucket = 0; bucket <= MaxMilliseconds * BucketsPerMillisecond; bucket++)
        {
            accumulated += Volatile.Read(ref _histograms[stage, bucket]);
            if (accumulated >= rank) return bucket / (double)BucketsPerMillisecond;
        }
        return null; // 溢出区间不伪造尾延迟。
    }

    internal sealed record StageSummary(string Stage, long Count, long TotalTicks, long MaxTicks,
        double? MeanMilliseconds, double? MaxMilliseconds,
        double? P50Milliseconds, double? P95Milliseconds, double? P99Milliseconds,
        double LatencyBucketWidthMilliseconds, int LatencyHistogramMaxMilliseconds, long LatencyHistogramOverflowCount);
}

