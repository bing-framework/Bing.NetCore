using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>本探针无事务负载专用：只计时打开，不包装命令、参数或 Reader；不用于事务资源绑定。</summary>
internal sealed class SqlTimedDbConnection(DbConnection inner, SqlConnectionOpenTimings timings) : DbConnection
{
    public override string ConnectionString { get => inner.ConnectionString; set => inner.ConnectionString = value; }
    public override string Database => inner.Database;
    public override string DataSource => inner.DataSource;
    public override string ServerVersion => inner.ServerVersion;
    public override int ConnectionTimeout => inner.ConnectionTimeout;
    public override ConnectionState State => inner.State;

    public override void Open()
    {
        var started = Stopwatch.GetTimestamp();
        var success = false;
        try { inner.Open(); success = true; }
        finally { timings.Record(Stopwatch.GetTimestamp() - started, success); }
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var success = false;
        try { await inner.OpenAsync(cancellationToken).ConfigureAwait(false); success = true; }
        finally { timings.Record(Stopwatch.GetTimestamp() - started, success); }
    }

    public override void Close() => inner.Close();
    public override Task CloseAsync() => inner.CloseAsync();
    public override void ChangeDatabase(string databaseName) => inner.ChangeDatabase(databaseName);
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => inner.BeginTransaction(isolationLevel);
    // 原生命令仍绑定原生连接，使 Provider 的参数定制和 Dapper 分派保持可用。
    protected override DbCommand CreateDbCommand() => inner.CreateCommand();
    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
    public override ValueTask DisposeAsync() => inner.DisposeAsync();
}

/// <summary>固定四个累加槽位；包括失败和取消，不能解释为纯池排队或 CPU 时间。</summary>
internal sealed class SqlConnectionOpenTimings
{
    private long _count, _failed, _totalTicks, _maxTicks;

    internal void Record(long ticks, bool success)
    {
        Interlocked.Increment(ref _count);
        if (!success) Interlocked.Increment(ref _failed);
        Interlocked.Add(ref _totalTicks, ticks);
        for (var current = Volatile.Read(ref _maxTicks); ticks > current; current = Volatile.Read(ref _maxTicks))
            if (Interlocked.CompareExchange(ref _maxTicks, ticks, current) == current) break;
    }

    /// <summary>只在负载完成并 drain 后生成快照。</summary>
    internal Summary Snapshot()
    {
        var count = Volatile.Read(ref _count);
        var total = Volatile.Read(ref _totalTicks);
        var max = Volatile.Read(ref _maxTicks);
        return new Summary(count, Volatile.Read(ref _failed), total, max,
            count == 0 ? null : total * 1000d / Stopwatch.Frequency / count,
            count == 0 ? null : max * 1000d / Stopwatch.Frequency);
    }

    internal sealed record Summary(long Count, long Failed, long TotalTicks, long MaxTicks,
        double? MeanMilliseconds, double? MaxMilliseconds);
}
