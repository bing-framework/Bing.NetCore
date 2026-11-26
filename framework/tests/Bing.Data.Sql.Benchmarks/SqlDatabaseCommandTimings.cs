using System;
using System.IO;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>仅用于诊断的服务端累计统计；语句执行不包含客户端物化和隐式提交。</summary>
internal sealed record SqlDatabaseCommandTimings(SqlDatabaseStatementTiming Read,
    SqlDatabaseStatementTiming Write, SqlDatabaseWalTiming Wal, string StatementsReset)
{
    internal SqlDatabaseCommandTimings Since(SqlDatabaseCommandTimings before)
    {
        if (StatementsReset != before.StatementsReset || Wal.StatsReset != before.Wal.StatsReset)
            throw new InvalidDataException("Database statistics were reset during diagnosis.");
        return new(Read.Since(before.Read), Write.Since(before.Write), Wal.Since(before.Wal), StatementsReset);
    }

    internal void ValidateCounts(long reads, long writes)
    {
        if (Read.Calls != reads || Read.Rows != reads || Write.Calls != writes || Write.Rows != writes)
            throw new InvalidDataException("Database statement calls/rows differ from successful primary-key requests.");
    }

    internal static double Nonnegative(double value) => double.IsFinite(value) && value >= 0 ? value :
        throw new InvalidDataException("Database statistics decreased or contain an invalid value.");
    internal static long Nonnegative(long value) => value >= 0 ? value :
        throw new InvalidDataException("Database statistics decreased.");
    internal static decimal Nonnegative(decimal value) => value >= 0 ? value :
        throw new InvalidDataException("Database statistics decreased.");
}

/// <summary>按本次唯一表及 SELECT/UPDATE 分类，单位由属性名指定。</summary>
internal sealed record SqlDatabaseStatementTiming(long Calls, long Rows, double TotalExecutionMilliseconds,
    long SharedBlocksHit, long SharedBlocksRead, long SharedBlocksDirtied, long SharedBlocksWritten,
    double BlockReadMilliseconds, double BlockWriteMilliseconds, long WalRecords, decimal WalBytes)
{
    public double? MeanExecutionMilliseconds => Calls == 0 ? null : TotalExecutionMilliseconds / Calls;

    internal SqlDatabaseStatementTiming Since(SqlDatabaseStatementTiming before) => new(
        SqlDatabaseCommandTimings.Nonnegative(Calls - before.Calls),
        SqlDatabaseCommandTimings.Nonnegative(Rows - before.Rows),
        SqlDatabaseCommandTimings.Nonnegative(TotalExecutionMilliseconds - before.TotalExecutionMilliseconds),
        SqlDatabaseCommandTimings.Nonnegative(SharedBlocksHit - before.SharedBlocksHit),
        SqlDatabaseCommandTimings.Nonnegative(SharedBlocksRead - before.SharedBlocksRead),
        SqlDatabaseCommandTimings.Nonnegative(SharedBlocksDirtied - before.SharedBlocksDirtied),
        SqlDatabaseCommandTimings.Nonnegative(SharedBlocksWritten - before.SharedBlocksWritten),
        SqlDatabaseCommandTimings.Nonnegative(BlockReadMilliseconds - before.BlockReadMilliseconds),
        SqlDatabaseCommandTimings.Nonnegative(BlockWriteMilliseconds - before.BlockWriteMilliseconds),
        SqlDatabaseCommandTimings.Nonnegative(WalRecords - before.WalRecords),
        SqlDatabaseCommandTimings.Nonnegative(WalBytes - before.WalBytes));
}

/// <summary>整个专用 PostgreSQL 集群的异步累计 WAL 统计，不能作为逐请求提交时间。</summary>
internal sealed record SqlDatabaseWalTiming(long Records, decimal Bytes, long Writes, long Syncs,
    double WriteMilliseconds, double SyncMilliseconds, string StatsReset)
{
    internal SqlDatabaseWalTiming Since(SqlDatabaseWalTiming before) => new(
        SqlDatabaseCommandTimings.Nonnegative(Records - before.Records),
        SqlDatabaseCommandTimings.Nonnegative(Bytes - before.Bytes),
        SqlDatabaseCommandTimings.Nonnegative(Writes - before.Writes),
        SqlDatabaseCommandTimings.Nonnegative(Syncs - before.Syncs),
        SqlDatabaseCommandTimings.Nonnegative(WriteMilliseconds - before.WriteMilliseconds),
        SqlDatabaseCommandTimings.Nonnegative(SyncMilliseconds - before.SyncMilliseconds), StatsReset);
}
