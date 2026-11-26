using Bing.Data.Sql.Benchmarks;

internal static class SqlStageTimingTests
{
    internal static void Run()
    {
        EmptyStagesDoNotInventDurations();
        StagesKeepIndependentCountsAndTotals();
        ConcurrentRecordsAreNotLost();
        SnapshotsDoNotChangeAfterRecording();
        PercentilesUseNearestRankAndKeepStagesSeparate();
        OverflowDoesNotInventTailPercentiles();
        EmptyAndZeroSamplesHaveDifferentPercentiles();
        RecordingDoesNotAllocatePerSample();
        Console.WriteLine("SQL_STAGE_TIMING_TESTS_PASS (8)");
    }

    private static void EmptyStagesDoNotInventDurations()
    {
        var rows = new SqlStageTimings().Snapshot();
        Check(rows.Length == 4 && rows.All(x => x.Count == 0 && x.MeanMilliseconds == null && x.MaxMilliseconds == null),
            "空阶段必须保留空耗时，不能伪装成真实零耗时。");
    }

    private static void StagesKeepIndependentCountsAndTotals()
    {
        var timings = new SqlStageTimings();
        timings.Record(SqlRequestStage.Compose, System.Diagnostics.Stopwatch.Frequency);
        timings.Record(SqlRequestStage.Compose, 3 * System.Diagnostics.Stopwatch.Frequency);
        timings.Record(SqlRequestStage.ReadExecute, 0);
        var rows = timings.Snapshot();
        Check(rows[0].Count == 2 && rows[0].MeanMilliseconds == 2000 && rows[0].MaxMilliseconds == 3000 &&
            rows[1].Count == 0 && rows[2].Count == 1 && rows[2].MeanMilliseconds == 0,
            "阶段计数、均值、最大值独立；真实零样本不等于缺测。");
        Check(rows.Select(x => x.Stage).SequenceEqual(new[] { "scope-and-compose", "read-freeze-plan",
            "read-execute-and-materialize", "write-execute" }), "阶段名称及顺序稳定。");
    }

    private static void ConcurrentRecordsAreNotLost()
    {
        var timings = new SqlStageTimings();
        Parallel.For(0, 10000, _ => timings.Record(SqlRequestStage.Freeze, 7));
        var result = timings.Snapshot()[1];
        Check(result.Count == 10000 && result.TotalTicks == 70000 && result.MaxTicks == 7 &&
            result.P50Milliseconds == 0.1 && result.P99Milliseconds == 0.1 && result.LatencyHistogramOverflowCount == 0,
            "并发累加不丢失总量或分位数样本。");
    }

    private static void SnapshotsDoNotChangeAfterRecording()
    {
        var timings = new SqlStageTimings();
        var before = timings.Snapshot();
        timings.Record(SqlRequestStage.WriteExecute, 1);
        Check(before[3].Count == 0 && timings.Snapshot()[3].Count == 1, "返回独立快照。");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private static void PercentilesUseNearestRankAndKeepStagesSeparate()
    {
        var timings = new SqlStageTimings();
        for (var i = 0; i < 100; i++)
            timings.Record(SqlRequestStage.ReadExecute, System.Diagnostics.Stopwatch.Frequency / 1000 * (i < 50 ? 1 : i < 95 ? 2 : 3));
        timings.Record(SqlRequestStage.WriteExecute, System.Diagnostics.Stopwatch.Frequency / 100);
        var rows = timings.Snapshot();
        Check(rows[2].P50Milliseconds == 1 && rows[2].P95Milliseconds == 2 && rows[2].P99Milliseconds == 3 &&
            rows[3].P99Milliseconds == 10 && rows[2].LatencyBucketWidthMilliseconds == 0.1 &&
            rows[2].LatencyHistogramMaxMilliseconds == 100,
            "读写独立，使用固定0.1ms桶的nearest-rank上界；不保留逐请求样本。");
    }

    private static void OverflowDoesNotInventTailPercentiles()
    {
        var timings = new SqlStageTimings();
        timings.Record(SqlRequestStage.ReadExecute, System.Diagnostics.Stopwatch.Frequency / 10);
        timings.Record(SqlRequestStage.ReadExecute, System.Diagnostics.Stopwatch.Frequency);
        var row = timings.Snapshot()[2];
        Check(row.P50Milliseconds == 100 && row.P95Milliseconds == null && row.P99Milliseconds == null &&
            row.LatencyHistogramOverflowCount == 1 && row.MaxMilliseconds == 1000,
            "超100ms仍保留真实最大值，溢出分位数必须为空。");
    }

    private static void EmptyAndZeroSamplesHaveDifferentPercentiles()
    {
        var timings = new SqlStageTimings();
        timings.Record(SqlRequestStage.Compose, 0);
        var rows = timings.Snapshot();
        Check(rows[0].P99Milliseconds == 0 && rows[1].P50Milliseconds == null &&
            rows[1].P95Milliseconds == null && rows[1].P99Milliseconds == null,
            "空阶段不能伪装成零延迟，真实零耗时样本可以位于零桶。");
    }

    private static void RecordingDoesNotAllocatePerSample()
    {
        var timings = new SqlStageTimings();
        for (var i = 0; i < 10000; i++) timings.Record(SqlRequestStage.Freeze, i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) timings.Record(SqlRequestStage.Freeze, i);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "固定桶累加不能新增逐样本分配。");
        Check(timings.Snapshot()[1].Count == 20000, "分配验证必须真正记录全部样本。");
    }
}

