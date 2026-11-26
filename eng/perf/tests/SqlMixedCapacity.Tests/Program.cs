using Bing.Data.Sql.Benchmarks;

SqlDatabaseCommandTimingTests.Run();
await SqlTimedDbConnectionTests.RunAsync();
SqlStageTimingTests.Run();
SqlDatabaseWaitSampleTests.Run();
await StopsAdmissionAtWindowAndDrainsStartedOperations();
MaintainsTheConfiguredReadWriteRatio();
await BoundsConcurrencyAndCountsFailures();
ReportsEmptyWindowsWithoutPercentiles();
ReportsHistogramOverflowWithoutInventingTailPercentiles();
await RecordsUtcBoundariesThroughDrainForResourceAlignment();
Console.WriteLine("SQL_MIXED_CAPACITY_TESTS_PASS (6)");
await SqlPoolCounterTests.RunAsync();
if (args.Length == 1) await SqlPoolCounterTests.RunDriverContractAsync(args[0]);

static async Task StopsAdmissionAtWindowAndDrainsStartedOperations()
{
    var active = 0;
    var finished = 0;
    var result = await SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(30), 2, 90,
        1, async (_, _) =>
    {
        Interlocked.Increment(ref active);
        await Task.Delay(60);
        Interlocked.Decrement(ref active);
        Interlocked.Increment(ref finished);
    });
    Check(active == 0 && finished == result.Successful && result.CompletedAfterWindow > 0 &&
        result.WindowMilliseconds == 30,
        "窗口截止后停止派发，并等待已开始的操作完成。");
    Check(result.AdmissionRequestsPerSecond == result.Attempted / 0.03 &&
        result.SuccessfulCompletionsWithinWindowPerSecond == result.SuccessfulWithinWindow / 0.03 &&
        result.SuccessfulCompletionsIncludingDrainPerSecond ==
        result.Successful / (result.ElapsedIncludingDrainMilliseconds / 1000d) &&
        result.CommandTimeoutSeconds == 1,
        "报告窗口准入吞吐、含 drain 完成吞吐和命令超时口径。");
}

static async Task RecordsUtcBoundariesThroughDrainForResourceAlignment()
{
    var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var result = await SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(20), 1, 90,
        1, async (_, _) => await Task.Delay(45));
    var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    Check(result.StartedAtUnixMilliseconds >= before && result.StoppedAtUnixMilliseconds <= after &&
        result.StoppedAtUnixMilliseconds - result.StartedAtUnixMilliseconds >= 40 &&
        result.CompletedAfterWindow > 0,
        "UTC 边界必须覆盖测量和 drain，以便外部容器资源按同一窗口归集。");
}

static void MaintainsTheConfiguredReadWriteRatio()
{
    var writes = Enumerable.Range(0, 100).Count(SqlMixedCapacityLoad.IsWriteRequest);
    Check(writes == 10 && Enumerable.Range(0, 10).Select(SqlMixedCapacityLoad.IsWriteRequest).Count(value => value) == 1,
        "每十个请求固定调度一个写操作；窗口尾部实际比例按请求计数报告。");
}

static async Task BoundsConcurrencyAndCountsFailures()
{
    var active = 0;
    var peak = 0;
    var result = await SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(35), 2, 90,
        1, async (request, _) =>
    {
        var now = Interlocked.Increment(ref active);
        UpdatePeak(ref peak, now);
        await Task.Delay(1);
        Interlocked.Decrement(ref active);
        if (request == 0) throw new InvalidOperationException("probe failure");
    });
    Check(peak <= 2 && result.PeakInFlight <= 2 && result.Failed > 0 && result.FirstFailureType == typeof(InvalidOperationException).FullName &&
        result.ReadAttempts + result.WriteAttempts == result.Attempted && result.FailedReads + result.FailedWrites == result.Failed &&
        result.ActualWritePercent == result.WriteAttempts * 100d / result.Attempted,
        "并发不超过配置值，操作失败被记录且不中断窗口。");
}

static void ReportsEmptyWindowsWithoutPercentiles()
{
    var summary = SqlMixedCapacityLoad.SummarizeLatencies(
        new long[SqlMixedCapacityLoad.LatencyHistogramMaxMilliseconds + 1], 0, 0);
    Check(summary.P50Milliseconds == null && summary.P99Milliseconds == null,
        "没有成功请求时不报告延迟分位数。");
}

static void ReportsHistogramOverflowWithoutInventingTailPercentiles()
{
    var histogram = new long[SqlMixedCapacityLoad.LatencyHistogramMaxMilliseconds + 1];
    histogram[120_000] = 1;
    var summary = SqlMixedCapacityLoad.SummarizeLatencies(histogram, 2, 1);
    Check(SqlMixedCapacityLoad.LatencyBucketWidthMilliseconds == 1 &&
        summary.P50Milliseconds == 120_000 && summary.P99Milliseconds == null,
        "直方图使用 1ms 桶，溢出样本单独计数且尾分位数不伪装为 120 秒。");
}

static void UpdatePeak(ref int peak, int value)
{
    for (var current = Volatile.Read(ref peak); value > current; current = Volatile.Read(ref peak))
        if (Interlocked.CompareExchange(ref peak, value, current) == current) return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
