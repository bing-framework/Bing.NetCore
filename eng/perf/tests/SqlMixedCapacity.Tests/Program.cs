using Bing.Data.Sql.Benchmarks;
using System.Diagnostics;

var emptyWindow = RecordsUtcBoundariesWhenWorkerMissesWindow();
RejectsOnlyEmptySamples(emptyWindow);
VerifiesDelayedAdmission(StopsAdmissionAtWindowAndDrainsStartedOperations, "drain");
VerifiesDelayedAdmission(BoundsConcurrencyAndCountsFailures, "failure-count");
VerifiesThreadPoolRestoredWhenTestFails();

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
Console.WriteLine("SQL_MIXED_CAPACITY_TESTS_PASS (11)");
await SqlPoolCounterTests.RunAsync();
if (args.Length == 1) await SqlPoolCounterTests.RunDriverContractAsync(args[0]);

static async Task StopsAdmissionAtWindowAndDrainsStartedOperations(Action<SqlMixedCapacityLoad.Result> observe = null)
{
    var active = 0;
    var finished = 0;
    var result = await RunWithAdmission(() => SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(30), 2, 90,
        1, async (_, _) =>
    {
        Interlocked.Increment(ref active);
        await Task.Delay(60);
        Interlocked.Decrement(ref active);
        Interlocked.Increment(ref finished);
    }), observe);
    Check(active == 0 && finished == result.Successful && result.Successful == result.Attempted && result.Attempted <= 2 && result.CompletedAfterWindow > 0 &&
        result.WindowMilliseconds == 30,
        $"窗口截止后停止派发，并等待已开始的操作完成：attempted={result.Attempted}, finished={finished}, active={active}, drain={result.CompletedAfterWindow}");
    Check(result.AdmissionRequestsPerSecond == result.Attempted / 0.03 &&
        result.SuccessfulCompletionsWithinWindowPerSecond == result.SuccessfulWithinWindow / 0.03 &&
        result.SuccessfulCompletionsIncludingDrainPerSecond ==
        result.Successful / (result.ElapsedIncludingDrainMilliseconds / 1000d) &&
        result.CommandTimeoutSeconds == 1,
        "报告窗口准入吞吐、含 drain 完成吞吐和命令超时口径。");
}

static async Task<SqlMixedCapacityLoad.Result> RecordsUtcBoundariesThroughDrainForResourceAlignment()
{
    var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    long entered = 0, completed = 0;
    var result = await SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(20), 1, 90,
        1, async (_, _) =>
        {
            entered = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Task.Delay(45);
            completed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        });
    var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var observed = $"before={before}, start={result.StartedAtUnixMilliseconds}, stop={result.StoppedAtUnixMilliseconds}, after={after}, entered={entered}, completed={completed}, attempted={result.Attempted}, drain={result.CompletedAfterWindow}";
    Check(result.StartedAtUnixMilliseconds >= before && result.StoppedAtUnixMilliseconds >= result.StartedAtUnixMilliseconds &&
        result.StoppedAtUnixMilliseconds <= after,
        $"UTC 边界必须位于调用期间：{observed}");
    if (result.Attempted > 0)
        Check(entered >= result.StartedAtUnixMilliseconds && completed >= entered && completed <= result.StoppedAtUnixMilliseconds,
            $"UTC 边界必须覆盖已开始操作及其完成：{observed}");
    return result;
}

static SqlMixedCapacityLoad.Result RecordsUtcBoundariesWhenWorkerMissesWindow()
{
    ThreadPool.GetMinThreads(out var minWorkers, out var minIo);
    ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
    using var occupied = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    using var blockerDone = new ManualResetEventSlim();
    var blockerStarted = false;
    try
    {
        Check(ThreadPool.SetMinThreads(1, minIo) && ThreadPool.SetMaxThreads(1, maxIo), "无法限制测试线程池。");
        ThreadPool.QueueUserWorkItem(_ => { try { occupied.Set(); release.Wait(); } finally { blockerDone.Set(); } });
        occupied.Wait();
        blockerStarted = true;
        var test = RecordsUtcBoundariesThroughDrainForResourceAlignment();
        // 窗口开始后保持 worker 不可用，确定性覆盖零请求而不改变生产窗口。
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromMilliseconds(20)) Thread.SpinWait(100);
        release.Set();
        var result = test.GetAwaiter().GetResult();
        Check(result.Attempted == 0 && result.CompletedAfterWindow == 0,
            $"worker 错过窗口时应无请求：attempted={result.Attempted}, drain={result.CompletedAfterWindow}");
        return result;
    }
    finally
    {
        release.Set();
        if (blockerStarted) blockerDone.Wait();
        var maxRestored = ThreadPool.SetMaxThreads(maxWorkers, maxIo);
        var minRestored = ThreadPool.SetMinThreads(minWorkers, minIo);
        Check(maxRestored && minRestored, "UTC 测试未能恢复线程池设置。");
    }
}

static void MaintainsTheConfiguredReadWriteRatio()
{
    var writes = Enumerable.Range(0, 100).Count(SqlMixedCapacityLoad.IsWriteRequest);
    Check(writes == 10 && Enumerable.Range(0, 10).Select(SqlMixedCapacityLoad.IsWriteRequest).Count(value => value) == 1,
        "每十个请求固定调度一个写操作；窗口尾部实际比例按请求计数报告。");
}

static async Task BoundsConcurrencyAndCountsFailures(Action<SqlMixedCapacityLoad.Result> observe = null)
{
    var active = 0;
    var peak = 0;
    var requestZero = 0;
    var result = await RunWithAdmission(() => SqlMixedCapacityLoad.RunAsync(TimeSpan.FromMilliseconds(35), 2, 90,
        1, async (request, _) =>
    {
        var now = Interlocked.Increment(ref active);
        UpdatePeak(ref peak, now);
        await Task.Delay(1);
        Interlocked.Decrement(ref active);
        if (request == 0) { Interlocked.Increment(ref requestZero); throw new InvalidOperationException("probe failure"); }
    }), observe);
    Check(requestZero == 1 && active == 0 && peak <= 2 && result.PeakInFlight <= 2 && result.Failed == 1 && result.FailedReads == 1 && result.FailedWrites == 0 &&
        result.Successful + result.Failed == result.Attempted && result.FirstFailureType == typeof(InvalidOperationException).FullName &&
        result.ReadAttempts + result.WriteAttempts == result.Attempted && result.FailedReads + result.FailedWrites == result.Failed &&
        result.ActualWritePercent == result.WriteAttempts * 100d / result.Attempted,
        $"并发不超过配置值，操作失败被记录且不中断窗口：attempted={result.Attempted}, failed={result.Failed}, requestZero={requestZero}, peak={peak}, active={active}");
}

// 空窗口合法，但这些用例只有取得实际准入的样本才能验证 drain 和失败计数。
static async Task<SqlMixedCapacityLoad.Result> RunWithAdmission(Func<Task<SqlMixedCapacityLoad.Result>> run, Action<SqlMixedCapacityLoad.Result> observe)
{
    const int maximumSamples = 8;
    for (var sample = 1; sample <= maximumSamples; sample++)
    {
        var result = await run();
        observe?.Invoke(result);
        if (result.Attempted > 0) return result;
    }
    throw new InvalidOperationException($"连续 {maximumSamples} 个窗口均无请求准入，无法验证测试合同。");
}

static void RejectsOnlyEmptySamples(SqlMixedCapacityLoad.Result emptyWindow)
{
    var samples = 0;
    try
    {
        RunWithAdmission(() => { samples++; return Task.FromResult(emptyWindow); }, null).GetAwaiter().GetResult();
        throw new Exception("连续空窗口不能通过测试。");
    }
    catch (InvalidOperationException error)
    {
        Check(samples == 8 && error.Message.Contains("8"), $"空窗口取样必须有界且失败：samples={samples}");
    }
}

static void VerifiesDelayedAdmission(Func<Action<SqlMixedCapacityLoad.Result>, Task> test, string name)
{
    ThreadPool.GetMinThreads(out var minWorkers, out var minIo);
    ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
    using var occupied = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    using var blockerDone = new ManualResetEventSlim();
    var blockerStarted = false;
    var samples = new List<int>();
    try
    {
        Check(ThreadPool.SetMinThreads(1, minIo) && ThreadPool.SetMaxThreads(1, maxIo), "无法限制测试线程池。");
        ThreadPool.QueueUserWorkItem(_ => { try { occupied.Set(); release.Wait(); } finally { blockerDone.Set(); } });
        occupied.Wait();
        blockerStarted = true;
        var pending = test(result => samples.Add(result.Attempted));
        // 两个被测窗口分别为 30ms 和 35ms；从启动返回后等待最长窗口结束。
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromMilliseconds(35)) Thread.SpinWait(100);
        release.Set();
        pending.GetAwaiter().GetResult();
        Check(samples.Count >= 2 && samples[0] == 0 && samples[^1] > 0,
            $"{name} 必须在首个空窗口后取得有效样本：{string.Join(",", samples)}");
        Console.WriteLine($"SQL_DELAYED_ADMISSION_PASS {name} samples={string.Join(",", samples)}");
    }
    finally
    {
        release.Set();
        if (blockerStarted) blockerDone.Wait();
        var maxRestored = ThreadPool.SetMaxThreads(maxWorkers, maxIo);
        var minRestored = ThreadPool.SetMinThreads(minWorkers, minIo);
        Check(maxRestored && minRestored, $"{name} 测试未能恢复线程池设置。");
    }
}

static void VerifiesThreadPoolRestoredWhenTestFails()
{
    ThreadPool.GetMinThreads(out var minWorkers, out var minIo);
    ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
    var expected = new InvalidOperationException("controlled test failure");
    try
    {
        VerifiesDelayedAdmission(_ => throw expected, "restore-on-failure");
        throw new Exception("受控失败未传播。");
    }
    catch (InvalidOperationException error) when (ReferenceEquals(error, expected))
    {
        ThreadPool.GetMinThreads(out var actualMinWorkers, out var actualMinIo);
        ThreadPool.GetMaxThreads(out var actualMaxWorkers, out var actualMaxIo);
        Check(actualMinWorkers == minWorkers && actualMinIo == minIo && actualMaxWorkers == maxWorkers && actualMaxIo == maxIo,
            $"异常后线程池设置未恢复：min={actualMinWorkers}/{actualMinIo}, max={actualMaxWorkers}/{actualMaxIo}");
        Console.WriteLine("SQL_THREAD_POOL_RESTORE_ON_FAILURE_PASS");
    }
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
