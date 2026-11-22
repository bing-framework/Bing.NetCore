using System.Collections;
using System.Data;
using System.Data.Common;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Sqlite.Tests.Integration.SqlQuery;

/// <summary>生成 Lambda 执行入口必须保留主异常及所有清理异常。</summary>
public sealed class SqliteGeneratedCleanupContractTest
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task GeneratedList_WhenExecutionAndCleanupFail_ShouldPreserveAllErrors(bool bindFailure, bool asynchronous)
    {
        using var host = CreateServiceProvider();
        var connection = new CleanupConnection();
        using var query = CreateQuery(host, connection);
        Action<DbCommand, IEnumerable<SqlParam>> binder = (_, _) =>
        {
            if (bindFailure)
                throw new InvalidOperationException("PRIMARY_BIND");
        };

        var error = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            if (asynchronous)
                await SqlGeneratedExecution.ToListAsync<CleanupRow>(query.From<CleanupRow>(), _ => new CleanupRow(),
                    binder, null, default);
            else
                SqlGeneratedExecution.ToList<CleanupRow>(query.From<CleanupRow>(), _ => new CleanupRow(), binder, null);
        });

        Assert.Equal(bindFailure
                ? new[] { "PRIMARY_BIND", "CLEANUP_DISPOSE" }
                : new[] { "PRIMARY_EXECUTE", "CLEANUP_CLOSE", "CLEANUP_DISPOSE" },
            error.Flatten().InnerExceptions.Select(item => item.Message));
        Assert.Equal(bindFailure ? 0 : 1, connection.Opens);
        Assert.Equal(bindFailure ? 0 : 1, connection.Closes);
        Assert.Equal(bindFailure ? 0 : 1, connection.Executions);
        Assert.Equal(1, connection.CommandDisposes);
    }

    [Fact]
    public void GeneratedList_WhenReaderAndCleanupFail_ShouldKeepExceptionIdentityAndOrder()
    {
        using var host = CreateServiceProvider();
        var connection = new CleanupConnection
        {
            ExecuteException = null,
            ReaderReadException = new InvalidOperationException("PRIMARY_READ"),
            ReaderDisposeException = new ApplicationException("CLEANUP_READER_DISPOSE")
        };
        using var query = CreateQuery(host, connection);

        var error = Assert.Throws<AggregateException>(() =>
            SqlGeneratedExecution.ToList<CleanupRow>(query.From<CleanupRow>(), _ => new CleanupRow(), BindNothing, null));

        Assert.Collection(error.InnerExceptions,
            item => Assert.Same(connection.ReaderReadException, item),
            item => Assert.Same(connection.ReaderDisposeException, item),
            item => Assert.Same(connection.CloseException, item),
            item => Assert.Same(connection.CommandDisposeException, item));
        Assert.Equal(1, connection.ReaderDisposes);
        Assert.Equal(1, connection.Closes);
        Assert.Equal(1, connection.CommandDisposes);
    }

    [Fact]
    public void GeneratedList_WhenOnlyCommandCleanupFails_ShouldRethrowTheCleanupException()
    {
        using var host = CreateServiceProvider();
        var cleanupError = new ApplicationException("CLEANUP_DISPOSE_ONLY");
        var connection = new CleanupConnection
        {
            ExecuteException = null,
            CloseException = null,
            CommandDisposeException = cleanupError
        };
        using var query = CreateQuery(host, connection);

        var error = Assert.Throws<ApplicationException>(() =>
            SqlGeneratedExecution.ToList<CleanupRow>(query.From<CleanupRow>(), _ => new CleanupRow(), BindNothing, null));

        Assert.Same(cleanupError, error);
        Assert.Equal(1, connection.Opens);
        Assert.Equal(1, connection.Closes);
        Assert.Equal(1, connection.ReaderDisposes);
        Assert.Equal(1, connection.CommandDisposes);
    }

    [Fact]
    public async Task GeneratedListAsync_WhenCancelled_ShouldInvokeCancelAndPreserveCleanupOrder()
    {
        using var host = CreateServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var cancellationError = new OperationCanceledException("PRIMARY_CANCEL", cancellation.Token);
        var connection = new CleanupConnection
        {
            CancellationSource = cancellation,
            AsyncExecutionException = cancellationError
        };
        using var query = CreateQuery(host, connection);

        var error = await Assert.ThrowsAsync<AggregateException>(() => SqlGeneratedExecution.ToListAsync<CleanupRow>(
            query.From<CleanupRow>(), _ => new CleanupRow(), BindNothing, null, cancellation.Token));

        Assert.Collection(error.InnerExceptions,
            item => Assert.Same(cancellationError, item),
            item => Assert.Same(connection.CloseException, item),
            item => Assert.Same(connection.CommandDisposeException, item));
        Assert.Equal(1, connection.CancelCalls);
        Assert.Equal(1, connection.Closes);
        Assert.Equal(1, connection.CommandDisposes);
    }

    [Fact]
    public void GeneratedList_WhenUsingCallerOpenConnectionAndSharedTransaction_ShouldLeaveBothOwnedByCaller()
    {
        using var host = CreateServiceProvider();
        var connection = new CleanupConnection(initiallyOpen: true)
        {
            ExecuteException = null,
            CloseException = null,
            CommandDisposeException = null
        };
        var transaction = new CleanupTransaction(connection);
        using (var query = CreateQuery(host, connection))
        {
            SqlQueryRuntimeBinding.BindExternalTransactionResolver(query, () => transaction);

            var rows = SqlGeneratedExecution.ToList<CleanupRow>(query.From<CleanupRow>(), _ => new CleanupRow(),
                BindNothing, null);

            Assert.Empty(rows);
        }

        Assert.Same(transaction, connection.CommandTransaction);
        Assert.Equal(0, connection.Opens);
        Assert.Equal(0, connection.Closes);
        Assert.Equal(0, connection.ConnectionDisposes);
        Assert.Equal(0, transaction.Disposes);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.AddSqliteProvider();
        return services.BuildServiceProvider();
    }

    private static SqliteSqlQuery CreateQuery(IServiceProvider host, CleanupConnection connection)
    {
        var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite
        });
        SqlQueryRuntimeBinding.BindExternalConnection(query, connection, SqlConnectionSource.External);
        return query;
    }

    private static void BindNothing(DbCommand command, IEnumerable<SqlParam> parameters) { }
}

public sealed class CleanupRow
{
    public int Id { get; set; }
}

public sealed class CleanupConnection(bool initiallyOpen = false) : DbConnection
{
    private ConnectionState _state = initiallyOpen ? ConnectionState.Open : ConnectionState.Closed;

    public int Opens;
    public int Closes;
    public int Executions;
    public int CommandDisposes;
    public int ReaderDisposes;
    public int CancelCalls;
    public int ConnectionDisposes;
    public override string ConnectionString { get; set; } = "Data Source=:memory:";
    public override string Database => "main";
    public override string DataSource => ":memory:";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;
    public Exception ExecuteException { get; set; } = new InvalidOperationException("PRIMARY_EXECUTE");
    public Exception ReaderReadException { get; set; }
    public Exception ReaderDisposeException { get; set; }
    public Exception CloseException { get; set; } = new ApplicationException("CLEANUP_CLOSE");
    public Exception CommandDisposeException { get; set; } = new ApplicationException("CLEANUP_DISPOSE");
    public Exception AsyncExecutionException { get; set; }
    public CancellationTokenSource CancellationSource { get; set; }
    public IDbTransaction CommandTransaction { get; set; }

    public override void Open()
    {
        Opens++;
        _state = ConnectionState.Open;
    }

    public override void Close()
    {
        Closes++;
        _state = ConnectionState.Closed;
        if (CloseException != null)
            throw CloseException;
    }

    public override void ChangeDatabase(string name) => throw new NotSupportedException();

    protected override DbTransaction BeginDbTransaction(IsolationLevel level) => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new CleanupCommand(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ConnectionDisposes++;
        base.Dispose(disposing);
    }
}

public sealed class CleanupCommand(CleanupConnection owner) : DbCommand
{
    private readonly Microsoft.Data.Sqlite.SqliteCommand _parameters = new();
    private DbTransaction _transaction;

    public override string CommandText { get; set; }
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection DbConnection { get; set; } = owner;
    protected override DbTransaction DbTransaction
    {
        get => _transaction;
        set
        {
            _transaction = value;
            owner.CommandTransaction = value;
        }
    }
    protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;

    public override void Cancel() => owner.CancelCalls++;
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => _parameters.CreateParameter();
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object ExecuteScalar() => throw new NotSupportedException();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        owner.Executions++;
        if (owner.ExecuteException != null)
            throw owner.ExecuteException;
        return new CleanupReader(owner);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
        CancellationToken cancellationToken)
    {
        owner.Executions++;
        if (owner.CancellationSource != null)
        {
            owner.CancellationSource.Cancel();
            return Task.FromException<DbDataReader>(owner.AsyncExecutionException ??
                new OperationCanceledException(cancellationToken));
        }
        if (owner.ExecuteException != null)
            return Task.FromException<DbDataReader>(owner.ExecuteException);
        return Task.FromResult<DbDataReader>(new CleanupReader(owner));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            owner.CommandDisposes++;
        base.Dispose(disposing);
        if (disposing && owner.CommandDisposeException != null)
            throw owner.CommandDisposeException;
    }
}

public sealed class CleanupReader : DbDataReader
{
    private readonly CleanupConnection _owner;
    private readonly DbDataReader _inner = new DataTable().CreateDataReader();

    public CleanupReader(CleanupConnection owner) => _owner = owner;

    public override object this[int ordinal] => _inner[ordinal];
    public override object this[string name] => _inner[name];
    public override int Depth => _inner.Depth;
    public override int FieldCount => _inner.FieldCount;
    public override bool HasRows => _inner.HasRows;
    public override bool IsClosed => _inner.IsClosed;
    public override int RecordsAffected => _inner.RecordsAffected;
    public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) =>
        _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) =>
        _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
    public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => _inner.GetName(ordinal);
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public override string GetString(int ordinal) => _inner.GetString(ordinal);
    public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
    public override int GetValues(object[] values) => _inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
    public override bool NextResult() => _inner.NextResult();

    public override bool Read()
    {
        if (_owner.ReaderReadException != null)
            throw _owner.ReaderReadException;
        return _inner.Read();
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public override void Close() => _inner.Close();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _owner.ReaderDisposes++;
        _inner.Dispose();
        base.Dispose(disposing);
        if (disposing && _owner.ReaderDisposeException != null)
            throw _owner.ReaderDisposeException;
    }
}

public sealed class CleanupTransaction(DbConnection connection) : DbTransaction
{
    public int Disposes { get; private set; }
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() => throw new NotSupportedException();
    public override void Rollback() => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Disposes++;
        base.Dispose(disposing);
    }
}
