using Bing.Data.Sql.Benchmarks;

await MeasuresBoundedConcurrencyAndVisitsEachRequestOnce();
await CountsSynchronousAndAsynchronousFailures();
await ReportsNullPercentilesWhenAllRequestsFail();
UsesNearestRankForSuccessfulSamples();
Console.WriteLine("SQL_CAPACITY_LOAD_TESTS_PASS (4)");

static async Task MeasuresBoundedConcurrencyAndVisitsEachRequestOnce()
{
    var calls = new int[12];
    var firstWave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = 0;
    var run = SqlCapacityLoad.RunAsync(calls.Length, 2, async request =>
    {
        Interlocked.Increment(ref calls[request]);
        if (Interlocked.Increment(ref started) == 2)
            firstWave.SetResult();
        await release.Task;
    });
    try
    {
        await firstWave.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check(Volatile.Read(ref started) == 2, "只有两个并发名额可以进入执行。");
    }
    finally
    {
        release.SetResult();
    }
    var result = await run;
    Check(result.PeakInFlight == 2 && result.Successful == 12 && result.Failed == 0 && calls.All(count => count == 1),
        "全部请求应各执行一次且实际并发峰值为二。");
}

static async Task CountsSynchronousAndAsynchronousFailures()
{
    var result = await SqlCapacityLoad.RunAsync(7, 3, request => request switch
    {
        1 => throw new InvalidOperationException("sync"),
        4 => Task.FromException(new TimeoutException("async")),
        _ => Task.CompletedTask
    });
    Check(result.Successful == 5 && result.Failed == 2 && result.SuccessfulRequestsPerSecond > 0 &&
        result.P50Milliseconds.HasValue && result.P99Milliseconds.HasValue && result.FirstFailureType != null,
        "同步和异步异常都计入失败，其余请求继续运行并产生延迟样本。");
}

static async Task ReportsNullPercentilesWhenAllRequestsFail()
{
    var result = await SqlCapacityLoad.RunAsync(3, 2, _ => Task.FromException(new TimeoutException("internal detail")));
    Check(result.Successful == 0 && result.Failed == 3 && result.SuccessfulRequestsPerSecond == 0 &&
        result.P50Milliseconds == null && result.P95Milliseconds == null && result.P99Milliseconds == null &&
        result.FirstFailureType == typeof(TimeoutException).FullName, "全失败应保留失败类型，成功延迟为空。");
}

static void UsesNearestRankForSuccessfulSamples()
{
    var samples = Enumerable.Range(1, 100).Reverse().Select(i => (double)i).Append(double.NaN).ToArray();
    var summary = SqlCapacityLoad.SummarizeLatencies(samples);
    Check(summary == (100, 50d, 95d, 99d), "nearest-rank 只使用成功样本，排序和分母必须正确。");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException(description);
}
