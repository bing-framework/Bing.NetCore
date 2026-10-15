using System.Data;
using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Bing.FreeSQL.Events;
using Bing.Test.Shared;
using FreeSql;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证 MySQL 有序处理失败的锁定边界和只读观测。
/// </summary>
[Collection("MySqlOrderedEvents")]
public sealed class OrderedEventAtomicFailureMySqlTest
{
    /// <summary>
    /// 验证失败标记提交前仍锁定实体且另一消费者不能自动执行业务。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ProcessNextAsync_WhenFailureIsWaitingToCommit_ShouldKeepCursorLocked()
    {
        await WithDatabaseAsync(async (connection, orm, consumer, stream) =>
        {
            var store = new DbOrderedEventStore(OrderedEventDatabase.MySql);
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            var ordinary = new OrderedEventCoordinator(factory, store);
            await ordinary.ReceiveAsync(consumer, stream, new OrderedEventMessage("first", 1, "one"));
            var boundary = new Boundary();
            var failing = new OrderedEventCoordinator(new PausedFactory(factory, boundary), store);
            var original = new InvalidOperationException("business failure");
            var attempt = failing.ProcessNextAsync(consumer, stream, async (message, transaction, token) =>
            {
                await WriteBusinessAsync(transaction, stream, message, token);
                throw original;
            });
            var competing = new OrderedEventCoordinator(new ConnectionFactory(
                IntegrationTestConnectionStringResolver.Resolve("MySql")), store);
            var competingCalls = 0;
            try
            {
                await boundary.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
                var locked = await Assert.ThrowsAsync<MySqlException>(() => competing.ProcessNextAsync(consumer, stream,
                    (_, _, _) =>
                    {
                        Interlocked.Increment(ref competingCalls);
                        return Task.CompletedTask;
                    }));
                Assert.Equal(1205, locked.Number);
                Assert.Equal(0, competingCalls);
                Assert.Equal(TransactionOutcome.RolledBack, locked.GetTransactionOutcome());
            }
            finally
            {
                boundary.Release.TrySetResult(true);
                // 断言失败时也等待本测试持有的事务完成，不能遗留活动连接。
                await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
            }
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
            Assert.Same(original, failure);
            Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
            Assert.Equal(true, failure.Data["Bing.OrderedEventBusinessRolledBack"]);
            Assert.Equal(true, failure.Data["Bing.OrderedEventFailureRecorded"]);
            Assert.Equal(0, await CountBusinessAsync(connection, stream));
            var state = await ordinary.GetStateAsync(consumer, stream);
            Assert.True(state.Failed);
            Assert.Equal(0, state.LastSequence);
            Assert.Equal(1, state.Attempts);
            Assert.Equal(OrderedEventProcessingResult.Failed,
                await competing.ProcessNextAsync(consumer, stream, (_, _, _) =>
                {
                    Interlocked.Increment(ref competingCalls);
                    return Task.CompletedTask;
                }));
            Assert.Equal(0, competingCalls);
            await ordinary.RetryAsync(consumer, stream);
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await competing.ProcessNextAsync(consumer, stream,
                    (message, transaction, token) => WriteBusinessAsync(transaction, stream, message, token)));
            Assert.Equal(1, await CountBusinessAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证只读状态查询不等待活动业务写锁且不创建未知流。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task GetStateAsync_WhenBusinessHoldsWriteLock_ShouldReadCommittedSnapshotWithoutWriting()
    {
        await WithDatabaseAsync(async (connection, orm, consumer, stream) =>
        {
            var store = new DbOrderedEventStore(OrderedEventDatabase.MySql);
            var coordinator = new OrderedEventCoordinator(new FreeSqlOrderedEventTransactionFactory(orm), store);
            await coordinator.ReceiveAsync(consumer, stream, new OrderedEventMessage("first", 1, "one"));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var processing = coordinator.ProcessNextAsync(consumer, stream, async (message, transaction, token) =>
            {
                await WriteBusinessAsync(transaction, stream, message, token);
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
            });
            var unknown = new OrderedEventStream(stream.TenantId, stream.Stream, "unknown");
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                // 普通 SELECT 可在只读事务中观察提交版本；不得强制当前读或请求写锁。
                using (var readOnly = new MySqlCommand("SET TRANSACTION READ ONLY", connection))
                    await readOnly.ExecuteNonQueryAsync(timeout.Token);
                await using var observation = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, timeout.Token);
                var state = await store.GetStateAsync(observation, consumer, stream, timeout.Token);
                Assert.Equal(0, state.LastSequence);
                Assert.Equal(1, state.PendingCount);
                Assert.Equal(1, state.FirstPendingSequence);
                var empty = await store.GetStateAsync(observation, consumer, unknown, timeout.Token);
                Assert.Equal(0, empty.LastSequence);
                Assert.Equal(0, empty.PendingCount);
                await observation.CommitAsync(timeout.Token);
                using var cursor = new MySqlCommand("SELECT COUNT(*) FROM bing_event_cursor WHERE StreamKey = @stream", connection);
                cursor.Parameters.AddWithValue("@stream", store.GetStreamKey(unknown));
                Assert.Equal(0, Convert.ToInt64(await cursor.ExecuteScalarAsync(timeout.Token)));
            }
            finally
            {
                release.TrySetResult(true);
                Assert.Equal(OrderedEventProcessingResult.Processed, await processing);
            }
            Assert.Equal(1, (await coordinator.GetStateAsync(consumer, stream)).LastSequence);
            Assert.Equal(1, await CountBusinessAsync(connection, stream));
        });
    }

    /// <summary>
    /// 在专用数据库中建立随机隔离业务流并清理自身数据。
    /// </summary>
    private static async Task WithDatabaseAsync(Func<MySqlConnection, IFreeSql, string, OrderedEventStream, Task> action)
    {
        var configuration = new MySqlConnectionStringBuilder(IntegrationTestConnectionStringResolver.Resolve("MySql"))
        {
            Pooling = false
        };
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(configuration.ConnectionString, "MySql");
        await using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql))
        {
            using var schema = new MySqlCommand(sql, connection);
            await schema.ExecuteNonQueryAsync();
        }
        using (var schema = new MySqlCommand("CREATE TABLE IF NOT EXISTS bing_order_atomic_business " +
                   "(StreamKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, EventId varchar(128) NOT NULL, " +
                   "PRIMARY KEY (StreamKey, EventId)) ENGINE=InnoDB", connection))
            await schema.ExecuteNonQueryAsync();
        var scope = Guid.NewGuid().ToString("N");
        var stream = new OrderedEventStream(scope, "failure", "item");
        var unknown = new OrderedEventStream(scope, "failure", "unknown");
        using var orm = new FreeSqlBuilder().UseConnectionString(DataType.MySql, configuration.ConnectionString).Build();
        try
        {
            await action(connection, orm, "consumer-" + scope, stream);
        }
        finally
        {
            var store = new DbOrderedEventStore(OrderedEventDatabase.MySql);
            foreach (var table in new[] { "bing_order_atomic_business", "bing_event_inbox", "bing_event_inbox_archive", "bing_event_cursor", "bing_event_sequence" })
            {
                using var cleanup = new MySqlCommand($"DELETE FROM {table} WHERE StreamKey IN (@stream, @unknown)", connection);
                cleanup.Parameters.AddWithValue("@stream", store.GetStreamKey(stream));
                cleanup.Parameters.AddWithValue("@unknown", store.GetStreamKey(unknown));
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// 在处理器收到的事务中写入可观测业务记录。
    /// </summary>
    private static async Task WriteBusinessAsync(DbTransaction transaction, OrderedEventStream stream,
        OrderedEventMessage message, CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO bing_order_atomic_business (StreamKey, EventId) VALUES (@stream, @id)";
        var key = command.CreateParameter();
        key.ParameterName = "@stream";
        key.Value = new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(stream);
        command.Parameters.Add(key);
        var id = command.CreateParameter();
        id.ParameterName = "@id";
        id.Value = message.EventId;
        command.Parameters.Add(id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取当前随机业务流的提交数量。
    /// </summary>
    private static async Task<long> CountBusinessAsync(MySqlConnection connection, OrderedEventStream stream)
    {
        using var command = new MySqlCommand("SELECT COUNT(*) FROM bing_order_atomic_business WHERE StreamKey = @stream", connection);
        command.Parameters.AddWithValue("@stream", new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(stream));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 控制提交或旧式外层回滚后的竞争边界。
    /// </summary>
    private sealed class Boundary
    {
        /// <summary>
        /// 已到达需要检查锁状态的边界。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 允许当前事务继续完成。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 等待测试放行当前事务。
        /// </summary>
        internal async Task WaitAsync()
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    /// <summary>
    /// 在生产 FreeSQL 事务的提交边界暂停。
    /// </summary>
    private sealed class PausedFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 生产事务工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;
        /// <summary>
        /// 当前测试边界。
        /// </summary>
        private readonly Boundary _boundary;

        /// <summary>
        /// 初始化一个 <see cref="PausedFactory" /> 类型的实例。
        /// </summary>
        internal PausedFactory(IOrderedEventTransactionFactory inner, Boundary boundary) => (_inner, _boundary) = (inner, boundary);

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            new PausedTransaction(await _inner.BeginAsync(cancellationToken), _boundary);
    }

    /// <summary>
    /// 保留真实事务并暴露新旧实现的竞争边界。
    /// </summary>
    private sealed class PausedTransaction : IOrderedEventTransaction
    {
        /// <summary>
        /// 生产事务。
        /// </summary>
        private readonly IOrderedEventTransaction _inner;
        /// <summary>
        /// 当前测试边界。
        /// </summary>
        private readonly Boundary _boundary;

        /// <summary>
        /// 初始化一个 <see cref="PausedTransaction" /> 类型的实例。
        /// </summary>
        internal PausedTransaction(IOrderedEventTransaction inner, Boundary boundary) => (_inner, _boundary) = (inner, boundary);
        /// <inheritdoc />
        public DbTransaction Transaction => _inner.Transaction;
        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _boundary.WaitAsync();
            await _inner.CommitAsync(cancellationToken);
        }
        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _inner.RollbackAsync(cancellationToken);
            await _boundary.WaitAsync();
        }
        /// <inheritdoc />
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    /// <summary>
    /// 为竞争节点创建独立且有界等待的 MySQL 连接。
    /// </summary>
    private sealed class ConnectionFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 不使用连接池的专用测试配置。
        /// </summary>
        private readonly string _connectionString;
        /// <summary>
        /// 初始化一个 <see cref="ConnectionFactory" /> 类型的实例。
        /// </summary>
        internal ConnectionFactory(string connectionString) => _connectionString =
            new MySqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                using var timeout = new MySqlCommand("SET SESSION innodb_lock_wait_timeout = 1", connection);
                await timeout.ExecuteNonQueryAsync(cancellationToken);
                return new ConnectionTransaction(connection, await connection.BeginTransactionAsync(cancellationToken));
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    /// <summary>
    /// 拥有竞争节点本次连接与事务。
    /// </summary>
    private sealed class ConnectionTransaction : IOrderedEventTransaction
    {
        /// <summary>
        /// 本次专用连接。
        /// </summary>
        private readonly MySqlConnection _connection;
        /// <summary>
        /// 本次数据库事务。
        /// </summary>
        private readonly MySqlTransaction _transaction;
        /// <summary>
        /// 初始化一个 <see cref="ConnectionTransaction" /> 类型的实例。
        /// </summary>
        internal ConnectionTransaction(MySqlConnection connection, MySqlTransaction transaction) =>
            (_connection, _transaction) = (connection, transaction);
        /// <inheritdoc />
        public DbTransaction Transaction => _transaction;
        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);
        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);
        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            try { await _transaction.DisposeAsync(); }
            finally { await _connection.DisposeAsync(); }
        }
    }
}
