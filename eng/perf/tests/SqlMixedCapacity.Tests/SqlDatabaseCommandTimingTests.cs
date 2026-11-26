using Bing.Data.Sql.Benchmarks;

internal static class SqlDatabaseCommandTimingTests
{
    internal static void Run()
    {
        SubtractsWarmupWithoutAveragingAverages();
        KeepsEmptyMeansUnknown();
        RejectsStatisticsReset();
        RejectsEvictedOrDecreasingCounters();
        RejectsRequestCountMismatch();
        Console.WriteLine("SQL_DATABASE_COMMAND_TIMING_TESTS_PASS (5)");
    }

    private static SqlDatabaseCommandTimings Sample(long n) => new(
        new(n, n, n * 2d, n * 3, n, n, n, n * 0.1, n * 0.2, n, n * 64m),
        new(n, n, n * 4d, n * 3, n, n, n, n * 0.1, n * 0.2, n, n * 64m),
        new(n, n * 64m, n, n, n * 0.3, n * 0.4, "wal-reset"), "statement-reset");

    private static void SubtractsWarmupWithoutAveragingAverages()
    {
        var before = Sample(10);
        var delta = Sample(13).Since(before);
        delta.ValidateCounts(3, 3);
        Check(delta.Read == Sample(3).Read && delta.Write == Sample(3).Write &&
            Math.Abs(delta.Wal.SyncMilliseconds - 1.2) < 1e-10 && delta.Wal.Bytes == 192 &&
            delta.Read.MeanExecutionMilliseconds == 2 && delta.Write.MeanExecutionMilliseconds == 4 &&
            before.Read.Calls == 10, "差值须排除预热、保留所有计数和独立快照。");
    }

    private static void KeepsEmptyMeansUnknown()
    {
        var delta = Sample(10).Since(Sample(10));
        delta.ValidateCounts(0, 0);
        Check(delta.Read.MeanExecutionMilliseconds == null && delta.Write.MeanExecutionMilliseconds == null,
            "没有执行时不能生成零均值。");
    }

    private static void RejectsStatisticsReset()
    {
        Reject(() => (Sample(13) with { StatementsReset = "changed" }).Since(Sample(10)));
        Reject(() => (Sample(13) with { Wal = Sample(13).Wal with { StatsReset = "changed" } }).Since(Sample(10)));
    }

    private static void RejectsEvictedOrDecreasingCounters()
    {
        Reject(() => Sample(9).Since(Sample(10)));
        Reject(() => (Sample(13) with { Read = Sample(13).Read with { TotalExecutionMilliseconds = double.NaN } }).Since(Sample(10)));
        Reject(() => (Sample(13) with { Wal = Sample(13).Wal with { WriteMilliseconds = double.PositiveInfinity } }).Since(Sample(10)));
    }

    private static void RejectsRequestCountMismatch()
    {
        Reject(() => Sample(13).Since(Sample(10)).ValidateCounts(2, 3));
        Reject(() => (Sample(3) with { Write = Sample(3).Write with { Rows = 2 } }).ValidateCounts(3, 3));
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("错误统计未被拒绝。");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
    }
}
