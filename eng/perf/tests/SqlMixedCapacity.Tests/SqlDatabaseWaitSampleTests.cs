using Bing.Data.Sql.Benchmarks;

internal static class SqlDatabaseWaitSampleTests
{
    internal static void Run()
    {
        EmptySamplesHaveZeroCounts();
        AccumulatesMutuallyExclusiveObservations();
        ZeroActiveStillCountsAsASample();
        RejectsNegativeCounts();
        RejectsInconsistentObservationTotals();
        SnapshotsRemainIndependent();
        Console.WriteLine("SQL_DATABASE_WAIT_SAMPLE_TESTS_PASS (6)");
    }

    private static void EmptySamplesHaveZeroCounts()
    {
        var summary = new SqlDatabaseWaitSamples().Snapshot();
        Check(summary.IntervalMilliseconds == 100 && summary.SampleCount == 0 &&
            summary.ActiveSessionObservations == 0 && summary.LockObservations == 0 &&
            summary.WalSyncObservations == 0 && summary.WalWriteObservations == 0 &&
            summary.OtherIoObservations == 0 && summary.NoWaitEventObservations == 0 &&
            summary.OtherWaitObservations == 0,
            "空采样应报告固定采样间隔和全零观测次数。");
    }

    private static void AccumulatesMutuallyExclusiveObservations()
    {
        var samples = new SqlDatabaseWaitSamples();
        samples.Record(10, 1, 2, 3, 1, 1, 2);
        samples.Record(4, 0, 1, 0, 1, 1, 1);
        var summary = samples.Snapshot();

        Check(summary.SampleCount == 2 && summary.ActiveSessionObservations == 14 &&
            summary.LockObservations == 1 && summary.WalSyncObservations == 3 &&
            summary.WalWriteObservations == 3 && summary.OtherIoObservations == 2 &&
            summary.NoWaitEventObservations == 2 && summary.OtherWaitObservations == 3,
            "每个采样区间的互斥类别应按观测次数分别累计。");
    }

    private static void ZeroActiveStillCountsAsASample()
    {
        var samples = new SqlDatabaseWaitSamples();
        samples.Record(0, 0, 0, 0, 0, 0, 0);
        var summary = samples.Snapshot();

        Check(summary.SampleCount == 1 && summary.ActiveSessionObservations == 0 &&
            summary.LockObservations == 0 && summary.WalSyncObservations == 0 &&
            summary.WalWriteObservations == 0 && summary.OtherIoObservations == 0 &&
            summary.NoWaitEventObservations == 0 && summary.OtherWaitObservations == 0,
            "零活动会话仍是一次采样，所有值均为观测次数。");
    }

    private static void RejectsNegativeCounts()
    {
        Reject(() => new SqlDatabaseWaitSamples().Record(-1, 0, 0, 0, 0, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, -1, 0, 0, 0, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 0, -1, 0, 0, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 0, 0, -1, 0, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 0, 0, 0, -1, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 0, 0, 0, 0, -1, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 0, 0, 0, 0, 0, -1));
    }

    private static void RejectsInconsistentObservationTotals()
    {
        Reject(() => new SqlDatabaseWaitSamples().Record(2, 1, 0, 0, 0, 0, 0));
        Reject(() => new SqlDatabaseWaitSamples().Record(1, 2, 0, 0, 0, 0, 0));
    }

    private static void SnapshotsRemainIndependent()
    {
        var samples = new SqlDatabaseWaitSamples();
        samples.Record(2, 1, 0, 0, 0, 0, 1);
        var before = samples.Snapshot();

        samples.Record(1, 0, 0, 0, 1, 0, 0);
        var after = samples.Snapshot();
        Check(before.SampleCount == 1 && before.ActiveSessionObservations == 2 &&
            before.LockObservations == 1 && before.OtherWaitObservations == 1 &&
            before.OtherIoObservations == 0 && after.SampleCount == 2 &&
            after.ActiveSessionObservations == 3 && after.OtherIoObservations == 1,
            "后续采样不得改变先前返回的独立快照。");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("无效采样数据未被拒绝。");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
    }
}
