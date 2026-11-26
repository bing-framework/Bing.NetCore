using System.Data;
using System.Data.Common;
using Bing.Data.Sql.Benchmarks;

internal static class SqlTimedDbConnectionTests
{
    internal static async Task RunAsync()
    {
        EmptyAndConcurrentOpenSamplesRemainBounded();
        OpensSynchronouslyAndPreservesProviderObjects();
        await OpensAsynchronouslyAndForwardsCancellation();
        await FailedAndCancelledOpensAreRecordedAndRethrown();
        await DisposesTheOwnedConnectionAsynchronously();
        Console.WriteLine("SQL_TIMED_CONNECTION_TESTS_PASS (5)");
    }

    private static void EmptyAndConcurrentOpenSamplesRemainBounded()
    {
        var timings = new SqlConnectionOpenTimings();
        var before = timings.Snapshot();
        Check(before.Count == 0 && before.MeanMilliseconds == null && before.MaxMilliseconds == null,
            "缺测不能伪装成零耗时。");
        Parallel.For(0, 10000, i => timings.Record(7, i % 2 == 0));
        var after = timings.Snapshot();
        Check(after.Count == 10000 && after.Failed == 5000 && after.TotalTicks == 70000 &&
            after.MaxTicks == 7 && before.Count == 0, "并发累加、失败数和独立快照必须一致。");
    }

    private static void OpensSynchronouslyAndPreservesProviderObjects()
    {
        var inner = new StubConnection();
        var timings = new SqlConnectionOpenTimings();
        using (var connection = new SqlTimedDbConnection(inner, timings))
        {
            connection.ConnectionString = "fixture";
            connection.Open();
            using var command = connection.CreateCommand();
            using var transaction = connection.BeginTransaction();
            Check(ReferenceEquals(command, inner.Command) && ReferenceEquals(command.Connection, inner) &&
                ReferenceEquals(transaction, inner.Transaction), "保留原生 Provider 命令、连接和事务。");
            Check(connection.State == ConnectionState.Open && connection.ConnectionString == "fixture" &&
                connection.Database == "fixture-db" && connection.DataSource == "fixture-server" &&
                connection.ServerVersion == "1" && connection.ConnectionTimeout == 3, "连接属性直接转发。");
            connection.Close();
            Check(inner.State == ConnectionState.Closed && timings.Snapshot().Count == 1 &&
                timings.Snapshot().Failed == 0, "成功打开仅记录一次，关闭不增加样本。");
        }
        Check(inner.DisposeCalls == 1, "同步释放拥有的底层连接。");
    }

    private static async Task OpensAsynchronouslyAndForwardsCancellation()
    {
        var inner = new StubConnection { WaitForOpen = true };
        var timings = new SqlConnectionOpenTimings();
        await using var connection = new SqlTimedDbConnection(inner, timings);
        using var cancellation = new CancellationTokenSource();
        var open = connection.OpenAsync(cancellation.Token);
        Check(!open.IsCompleted && timings.Snapshot().Count == 0, "异步打开完成前不发布耗时。");
        inner.CompleteOpen();
        await open;
        await connection.CloseAsync();
        Check(inner.LastToken == cancellation.Token && timings.Snapshot().Count == 1 &&
            timings.Snapshot().Failed == 0 && inner.State == ConnectionState.Closed,
            "传递取消令牌、等待真实打开和关闭，并只记录一次。");
    }

    private static async Task FailedAndCancelledOpensAreRecordedAndRethrown()
    {
        var failure = new InvalidOperationException("fixture-open-failure");
        var inner = new StubConnection { Failure = failure };
        var timings = new SqlConnectionOpenTimings();
        await using var connection = new SqlTimedDbConnection(inner, timings);
        try { connection.Open(); throw new Exception("Expected sync failure."); }
        catch (InvalidOperationException error) { Check(ReferenceEquals(error, failure), "保留原始同步异常。"); }
        try { await connection.OpenAsync(default); throw new Exception("Expected async failure."); }
        catch (InvalidOperationException error) { Check(ReferenceEquals(error, failure), "保留原始异步异常。"); }
        inner.Failure = null;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await connection.OpenAsync(cancelled.Token); throw new Exception("Expected cancellation."); }
        catch (OperationCanceledException error) { Check(error.CancellationToken == cancelled.Token, "保留取消令牌。"); }
        Check(timings.Snapshot().Count == 3 && timings.Snapshot().Failed == 3,
            "异常和取消也应计时，不能只报告成功打开。");
    }

    private static async Task DisposesTheOwnedConnectionAsynchronously()
    {
        var inner = new StubConnection();
        await using (var connection = new SqlTimedDbConnection(inner, new SqlConnectionOpenTimings()))
            await connection.OpenAsync(default);
        Check(inner.AsyncDisposeCalls == 1 && inner.DisposeCalls == 0, "异步释放应保留 Provider 异步路径。");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private sealed class StubConnection : DbConnection
    {
        private ConnectionState _state;
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception Failure;
        internal bool WaitForOpen;
        internal int DisposeCalls, AsyncDisposeCalls;
        internal CancellationToken LastToken;
        internal StubCommand Command;
        internal StubTransaction Transaction;
        public override string ConnectionString { get; set; }
        public override string Database => "fixture-db";
        public override string DataSource => "fixture-server";
        public override string ServerVersion => "1";
        public override int ConnectionTimeout => 3;
        public override ConnectionState State => _state;
        public override void Open() { if (Failure != null) throw Failure; _state = ConnectionState.Open; }
        public override async Task OpenAsync(CancellationToken token)
        {
            LastToken = token;
            token.ThrowIfCancellationRequested();
            if (WaitForOpen) await _open.Task.WaitAsync(token);
            Open();
        }
        internal void CompleteOpen() => _open.SetResult();
        public override void Close() => _state = ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            Transaction = new StubTransaction(this, isolationLevel);
        protected override DbCommand CreateDbCommand() => Command = new StubCommand(this);
        protected override void Dispose(bool disposing) { if (disposing) DisposeCalls++; }
        public override ValueTask DisposeAsync() { AsyncDisposeCalls++; return ValueTask.CompletedTask; }
    }

    private sealed class StubTransaction(DbConnection connection, IsolationLevel isolation) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => isolation;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class StubCommand : DbCommand
    {
        internal StubCommand(DbConnection connection) => DbConnection = connection;
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        protected override DbTransaction DbTransaction { get; set; }
        public override void Cancel() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }
}
