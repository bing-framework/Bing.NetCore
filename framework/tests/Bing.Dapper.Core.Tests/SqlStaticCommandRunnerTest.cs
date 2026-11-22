using System.Data;
using System.Data.Common;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 静态 SQL 命令执行器的 ADO.NET 资源所有权测试。
/// </summary>
public sealed class SqlStaticCommandRunnerTest
{
    /// <summary>
    /// 测试目的：命令执行前应绑定 SQL、类型、事务、超时和参数，且关闭执行器自行打开的连接。
    /// </summary>
    [Fact]
    public void Execute_WhenConnectionStartsClosed_ShouldBindCommandAndCloseOwnedConnection()
    {
        var connection = new TestDbConnection();
        var transaction = new TestDbTransaction(connection);
        var parameters = new RecordingParameterSource();
        var binderSawConfiguredCommand = false;

        parameters.OnBind = command =>
        {
            binderSawConfiguredCommand = command.CommandText == "select 1" &&
                command.CommandType == CommandType.StoredProcedure &&
                command.CommandTimeout == 37 && ReferenceEquals(command.Transaction, transaction);
        };

        var result = SqlStaticCommandRunner.Execute(connection, "select 1", parameters, transaction, 37, command =>
        {
            Assert.Equal("select 1", command.CommandText);
            Assert.Equal(CommandType.StoredProcedure, command.CommandType);
            Assert.Equal(37, command.CommandTimeout);
            Assert.Same(transaction, command.Transaction);
            Assert.Same(connection, command.Connection);
            return 42;
        }, CommandType.StoredProcedure);

        Assert.Equal(42, result);
        Assert.True(parameters.WasBound);
        Assert.True(binderSawConfiguredCommand);
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 测试目的：连接在调用前已打开时，执行器不得打开或关闭该连接。
    /// </summary>
    [Fact]
    public void Execute_WhenConnectionStartsOpen_ShouldLeaveConnectionOpen()
    {
        var connection = new TestDbConnection(ConnectionState.Open);

        var result = SqlStaticCommandRunner.Execute(connection, "select 1", null, null, null,
            _ => 5);

        Assert.Equal(5, result);
        Assert.Equal(0, connection.OpenCount);
        Assert.Equal(0, connection.CloseCount);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    /// <summary>
    /// 测试目的：参数绑定失败时应立即释放已创建命令，且不打开连接。
    /// </summary>
    [Fact]
    public void CreateCommand_WhenParameterBindingFails_ShouldDisposeCommand()
    {
        var connection = new TestDbConnection();
        var parameters = new RecordingParameterSource
        {
            ExceptionOnBind = new InvalidOperationException("binding failed")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlStaticCommandRunner.Execute(connection, "select 1", parameters, null, null, _ => 1));

        Assert.Equal("binding failed", exception.Message);
        Assert.True(parameters.WasBound);
        Assert.True(connection.LastCommand.IsDisposed);
        Assert.Equal(0, connection.OpenCount);
        Assert.Equal(0, connection.CloseCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>
    /// 测试目的：执行期间取消应把令牌传给动作，并释放命令及执行器打开的连接。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionObservesCancellation_ShouldReleaseCommandAndOwnedConnection()
    {
        var connection = new TestDbConnection();
        using var cancellation = new CancellationTokenSource();
        connection.OnOpen = cancellation.Cancel;
        var actionCalls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SqlStaticCommandRunner.ExecuteAsync(
            connection, "select 1", null, null, null, (_, token) =>
            {
                actionCalls++;
                return Task.FromCanceled<int>(token);
            }, cancellation.Token));

        Assert.Equal(1, actionCalls);
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(cancellation.Token, connection.OpenAsyncToken);
        Assert.True(connection.LastCommand.IsDisposed);
    }

    private sealed class RecordingParameterSource : ISqlStaticParameterSource
    {
        public bool WasBound { get; private set; }

        public Exception ExceptionOnBind { get; init; }

        public Action<DbCommand> OnBind { get; set; }

        public void BindTo(IDbCommand command)
        {
            WasBound = true;
            OnBind?.Invoke((DbCommand)command);
            if (ExceptionOnBind != null)
                throw ExceptionOnBind;
        }
    }

    private sealed class TestDbConnection : DbConnection
    {
        private ConnectionState _state;

        public TestDbConnection(ConnectionState initialState = ConnectionState.Closed)
        {
            _state = initialState;
        }

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public TestDbCommand LastCommand { get; private set; }

        public Action OnOpen { get; set; }

        public CancellationToken OpenAsyncToken { get; private set; }

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
        }

        public override void Open()
        {
            OpenCount++;
            _state = ConnectionState.Open;
            OnOpen?.Invoke();
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenAsyncToken = cancellationToken;
            _state = ConnectionState.Open;
            OnOpen?.Invoke();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            LastCommand = new TestDbCommand(this);
            return LastCommand;
        }
    }

    private sealed class TestDbCommand : DbCommand
    {
        private readonly DbConnection _connection;
        private DbTransaction _transaction;

        public TestDbCommand(DbConnection connection)
        {
            _connection = connection;
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

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed |= disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class TestDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public TestDbTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => _connection;

        public override void Commit() => throw new NotSupportedException();

        public override void Rollback() => throw new NotSupportedException();
    }
}
