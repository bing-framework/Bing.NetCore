using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证静态 SQL 执行与结果读取清理期间的异常保真和资源释放顺序。
/// </summary>
public sealed class SqlStaticCommandRunnerCleanupTest
{
    /// <summary>
    /// 绑定失败时，即使命令释放也失败，绑定异常仍排在清理异常之前。
    /// </summary>
    [Fact]
    public void CreateCommand_WhenBindingAndDisposeFail_ShouldAggregateBothExceptions()
    {
        var bindException = new InvalidOperationException("bind");
        var disposeException = new ApplicationException("command dispose");
        var connection = new TestConnection { CommandDisposeException = disposeException };

        var exception = Assert.Throws<AggregateException>(() => SqlStaticCommandRunner.CreateCommand(connection,
            "select 1", new FaultingParameterSource(bindException), null, null));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(bindException, current),
            current => Assert.Same(disposeException, current));
        Assert.True(connection.LastCommand.IsDisposed);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>
    /// 执行、连接关闭和命令释放均失败时仍尝试全部清理，并按时间顺序保留异常。
    /// </summary>
    [Fact]
    public void Execute_WhenActionAndAllCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var actionException = new InvalidOperationException("execute");
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = Assert.Throws<AggregateException>(() => SqlStaticCommandRunner.Execute<int>(connection, "select 1",
            null, null, null, _ => throw actionException));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(actionException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 同步打开失败时仍尝试关闭连接并释放命令。
    /// </summary>
    [Fact]
    public void Execute_WhenOpenAndCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var openException = new InvalidOperationException("open");
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            OpenException = openException,
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = Assert.Throws<AggregateException>(() => SqlStaticCommandRunner.Execute(connection, "select 1",
            null, null, null, _ => 1));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(openException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 只有清理失败时也必须报告所有清理异常。
    /// </summary>
    [Fact]
    public void Execute_WhenOnlyCleanupFails_ShouldAggregateAndContinueCleanup()
    {
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = Assert.Throws<AggregateException>(() => SqlStaticCommandRunner.Execute(connection, "select 1",
            null, null, null, _ => 1));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 异步打开失败时仍尝试关闭连接和释放命令，并保留打开异常为首个异常。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenOpenAndCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var openException = new InvalidOperationException("open");
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            OpenException = openException,
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => SqlStaticCommandRunner.ExecuteAsync(connection,
            "select 1", null, null, null, (_, _) => Task.FromResult(1), CancellationToken.None));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(openException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 异步执行失败时仍尝试关闭连接并释放命令。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionAndCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var actionException = new InvalidOperationException("execute async");
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => SqlStaticCommandRunner.ExecuteAsync(connection,
            "select 1", null, null, null, (_, _) => Task.FromException<int>(actionException),
            CancellationToken.None));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(actionException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 异步操作成功但清理失败时也必须报告全部清理异常。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenOnlyCleanupFails_ShouldAggregateAndContinueCleanup()
    {
        var closeException = new ApplicationException("connection close");
        var disposeException = new ObjectDisposedException("command");
        var connection = new TestConnection
        {
            CloseException = closeException,
            CommandDisposeException = disposeException
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => SqlStaticCommandRunner.ExecuteAsync(connection,
            "select 1", null, null, null, (_, _) => Task.FromResult(1), CancellationToken.None));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(closeException, current),
            current => Assert.Same(disposeException, current));
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 同步 Reader 失败后仍释放 Reader、连接及命令，且保留所有异常顺序。
    /// </summary>
    [Fact]
    public void Execute_WhenReaderAndEveryCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var readException = new InvalidOperationException("read");
        var readerDisposeException = new ApplicationException("reader dispose");
        var closeException = new ApplicationException("connection close");
        var commandDisposeException = new ObjectDisposedException("command");
        var reader = new TestDataReader
        {
            ReadException = readException,
            DisposeException = readerDisposeException
        };
        var connection = new TestConnection
        {
            Reader = reader,
            CloseException = closeException,
            CommandDisposeException = commandDisposeException
        };

        var exception = Assert.Throws<AggregateException>(() => SqlStaticCommandRunner.Execute(connection, "select 1",
            null, null, null, (command, cleanupExceptions) =>
            {
                var resultReader = command.ExecuteReader();
                return SqlExecutionBase.ExecuteWithReaderCleanup((DbDataReader)resultReader, cleanupExceptions,
                    currentReader =>
                    {
                        while (currentReader.Read()) { }
                        return 1;
                    });
            }));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(readException, current),
            current => Assert.Same(readerDisposeException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(commandDisposeException, current));
        Assert.True(reader.IsDisposed);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 异步 Reader 失败后仍释放 Reader、连接及命令，且保留所有异常顺序。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenReaderAndEveryCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var readException = new InvalidOperationException("read async");
        var readerDisposeException = new ApplicationException("reader dispose");
        var closeException = new ApplicationException("connection close");
        var commandDisposeException = new ObjectDisposedException("command");
        var reader = new TestDataReader
        {
            ReadException = readException,
            DisposeException = readerDisposeException
        };
        var connection = new TestConnection
        {
            Reader = reader,
            CloseException = closeException,
            CommandDisposeException = commandDisposeException
        };

        var exception = await Assert.ThrowsAsync<AggregateException>(() => SqlStaticCommandRunner.ExecuteAsync(connection,
            "select 1", null, null, null, (command, token, cleanupExceptions) =>
            {
                var resultReader = command.ExecuteReader();
                return SqlExecutionBase.ExecuteWithReaderCleanupAsync((DbDataReader)resultReader, cleanupExceptions,
                    async (currentReader, cancellationToken) =>
                    {
                        while (await currentReader.ReadAsync(cancellationToken).ConfigureAwait(false)) { }
                        return 1;
                    }, token);
            }, CancellationToken.None));

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(readException, current),
            current => Assert.Same(readerDisposeException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(commandDisposeException, current));
        Assert.True(reader.IsDisposed);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 静态流在读取失败时仍释放 Reader、连接和命令，并把读取异常放在清理异常之前。
    /// </summary>
    [Fact]
    public void StreamStaticRows_WhenReadAndEveryCleanupFail_ShouldAggregateInExecutionOrder()
    {
        var readException = new InvalidOperationException("stream read");
        var readerDisposeException = new ApplicationException("reader dispose");
        var closeException = new ApplicationException("connection close");
        var commandDisposeException = new ObjectDisposedException("command");
        var reader = new TestDataReader
        {
            ReadException = readException,
            DisposeException = readerDisposeException
        };
        var connection = new TestConnection
        {
            Reader = reader,
            CloseException = closeException,
            CommandDisposeException = commandDisposeException
        };
        var stream = SqlQueryBase.StreamStaticRows(connection, "select 1", null, null, null,
            new SqlStaticResultReader<int>(null, null, null), CommandType.Text);

        var exception = Assert.Throws<AggregateException>(() => stream.GetEnumerator().MoveNext());

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(readException, current),
            current => Assert.Same(readerDisposeException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(commandDisposeException, current));
        Assert.True(reader.IsDisposed);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 静态流正常结束但多项清理失败时仍尝试并报告每项清理异常。
    /// </summary>
    [Fact]
    public void StreamStaticRows_WhenOnlyCleanupFails_ShouldAggregateAndContinueCleanup()
    {
        var readerDisposeException = new ApplicationException("reader dispose");
        var closeException = new ApplicationException("connection close");
        var commandDisposeException = new ObjectDisposedException("command");
        var reader = new TestDataReader { DisposeException = readerDisposeException };
        var connection = new TestConnection
        {
            Reader = reader,
            CloseException = closeException,
            CommandDisposeException = commandDisposeException
        };
        using var enumerator = SqlQueryBase.StreamStaticRows(connection, "select 1", null, null, null,
            new SqlStaticResultReader<int>(null, null, null), CommandType.Text).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        var exception = Assert.Throws<AggregateException>(() => enumerator.MoveNext());

        Assert.Collection(exception.InnerExceptions,
            current => Assert.Same(readerDisposeException, current),
            current => Assert.Same(closeException, current),
            current => Assert.Same(commandDisposeException, current));
        Assert.True(reader.IsDisposed);
        Assert.Equal(1, connection.CloseCount);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    private sealed class FaultingParameterSource : ISqlStaticParameterSource
    {
        private readonly Exception _exception;

        public FaultingParameterSource(Exception exception) => _exception = exception;

        public void BindTo(IDbCommand command) => throw _exception;
    }

    private sealed class TestConnection : DbConnection
    {
        private ConnectionState _state;

        public Exception OpenException { get; init; }

        public Exception CloseException { get; init; }

        public Exception CommandDisposeException { get; init; }

        public DbDataReader Reader { get; init; }

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public TestCommand LastCommand { get; private set; }

        public override string ConnectionString { get; set; }

        public override string Database => "test";

        public override string DataSource => "test";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
            CloseCount++;
            _state = ConnectionState.Closed;
            if (CloseException != null) throw CloseException;
        }

        public override void Open()
        {
            OpenCount++;
            _state = ConnectionState.Open;
            if (OpenException != null) throw OpenException;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            _state = ConnectionState.Open;
            if (OpenException != null) throw OpenException;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            LastCommand = new TestCommand(this, Reader, CommandDisposeException);
            return LastCommand;
        }
    }

    private sealed class TestCommand : DbCommand
    {
        private readonly DbConnection _connection;
        private readonly DbDataReader _reader;
        private readonly Exception _disposeException;
        private DbTransaction _transaction;

        public TestCommand(DbConnection connection, DbDataReader reader, Exception disposeException)
        {
            _connection = connection;
            _reader = reader;
            _disposeException = disposeException;
        }

        public bool IsDisposed { get; private set; }

        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection
        {
            get => _connection;
            set => throw new NotSupportedException();
        }

        protected override DbParameterCollection DbParameterCollection => null;

        protected override DbTransaction DbTransaction
        {
            get => _transaction;
            set => _transaction = value;
        }

        public override void Cancel() => throw new NotSupportedException();

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare() => throw new NotSupportedException();

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _reader;

        protected override void Dispose(bool disposing)
        {
            IsDisposed |= disposing;
            if (disposing && _disposeException != null) throw _disposeException;
            base.Dispose(disposing);
        }
    }

    private sealed class TestDataReader : DbDataReader
    {
        private bool _read;

        public Exception ReadException { get; init; }

        public Exception DisposeException { get; init; }

        public bool IsDisposed { get; private set; }

        public override int Depth => 0;

        public override int FieldCount => 1;

        public override bool HasRows => true;

        public override bool IsClosed => IsDisposed;

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));

        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override string GetDataTypeName(int ordinal) => "INTEGER";

        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));

        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));

        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));

        public override Type GetFieldType(int ordinal) => typeof(int);

        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));

        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));

        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));

        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));

        public override string GetName(int ordinal) => ordinal == 0 ? "Value" : throw new IndexOutOfRangeException();

        public override int GetOrdinal(string name) => string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase)
            ? 0
            : throw new IndexOutOfRangeException(name);

        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal));

        public override object GetValue(int ordinal) => ordinal == 0 ? 42 : throw new IndexOutOfRangeException();

        public override int GetValues(object[] values)
        {
            if (values.Length == 0) return 0;
            values[0] = GetValue(0);
            return 1;
        }

        public override bool IsDBNull(int ordinal) => false;

        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();

        public override bool NextResult() => false;

        public override void Close() => Dispose();

        public override bool Read()
        {
            if (ReadException != null) throw ReadException;
            if (_read) return false;
            _read = true;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed |= disposing;
            if (disposing && DisposeException != null) throw DisposeException;
            base.Dispose(disposing);
        }
    }
}
