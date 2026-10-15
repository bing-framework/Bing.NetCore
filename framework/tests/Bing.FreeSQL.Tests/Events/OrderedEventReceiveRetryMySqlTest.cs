using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Bing.Test.Shared;
using Moq;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证有序事件接收仅重试已安全回滚的 MySQL 死锁。
/// </summary>
[Collection("MySqlOrderedEvents")]
public sealed class OrderedEventReceiveRetryMySqlTest
{
    /// <summary>
    /// 验证一次原生死锁后重试整个入箱事务。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenFirstAttemptDeadlocks_ShouldRetryAndInsertOnce()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var factory = new FaultingFactory(connectionString, getterFailures: 1);
            var coordinator = CreateCoordinator(factory);

            await coordinator.ReceiveAsync(consumer, stream, Message());

            Assert.Single(factory.InjectedErrors);
            Assert.Equal(1213, factory.InjectedErrors[0].Number);
            Assert.Equal(TransactionOutcome.RolledBack, factory.InjectedErrors[0].GetTransactionOutcome());
            Assert.Equal(2, factory.Begins);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Commits);
            Assert.Equal(2, factory.Disposals);
            Assert.Equal(1, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证四次死锁耗尽后保留最后一个原生异常。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenAllAttemptsDeadlock_ShouldThrowLastNativeError()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var factory = new FaultingFactory(connectionString, getterFailures: 4);

            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                CreateCoordinator(factory).ReceiveAsync(consumer, stream, Message()));

            Assert.Equal(4, factory.InjectedErrors.Count);
            Assert.Same(factory.InjectedErrors[3], failure);
            Assert.Equal(1213, failure.Number);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.Equal(4, factory.Begins);
            Assert.Equal(0, factory.Commits);
            Assert.Equal(4, factory.Rollbacks);
            Assert.Equal(4, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证释放失败时不重试已回滚的死锁。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenCleanupFails_ShouldNotRetry()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var cleanupFailure = new InvalidOperationException("cleanup failed");
            var factory = new FaultingFactory(connectionString, getterFailures: 1,
                disposeFailure: cleanupFailure);

            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                CreateCoordinator(factory).ReceiveAsync(consumer, stream, Message()));

            Assert.Same(factory.InjectedErrors[0], failure);
            Assert.Same(cleanupFailure, failure.Data["Bing.TransactionCleanupException"]);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.Equal(1, factory.Begins);
            Assert.Equal(0, factory.Commits);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证回滚后调用方取消会终止退避等待。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenCanceledAfterRollback_ShouldNotStartAnotherAttempt()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            using var cancellation = new CancellationTokenSource();
            var factory = new FaultingFactory(connectionString, getterFailures: 1,
                cancelAfterRollback: cancellation);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateCoordinator(factory).ReceiveAsync(consumer, stream, Message(), cancellation.Token));

            Assert.Single(factory.InjectedErrors);
            Assert.Equal(TransactionOutcome.RolledBack, factory.InjectedErrors[0].GetTransactionOutcome());
            Assert.Equal(1, factory.Begins);
            Assert.Equal(0, factory.Commits);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证提交阶段的原生死锁因结果未知而不重试。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenCommitSignalsDeadlock_ShouldNotRetryUnknownOutcome()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var factory = new FaultingFactory(connectionString, commitFailureNumber: 1213);

            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                CreateCoordinator(factory).ReceiveAsync(consumer, stream, Message()));

            Assert.Same(factory.InjectedErrors[0], failure);
            Assert.Equal(1213, failure.Number);
            Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
            Assert.Equal(1, factory.Begins);
            Assert.Equal(1, factory.Commits);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证锁等待超时不是可重试的死锁。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenLockWaitTimesOut_ShouldNotRetry()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var factory = new FaultingFactory(connectionString, getterFailures: 1, getterFailureNumber: 1205);

            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                CreateCoordinator(factory).ReceiveAsync(consumer, stream, Message()));

            Assert.Same(factory.InjectedErrors[0], failure);
            Assert.Equal(1205, failure.Number);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.Equal(1, factory.Begins);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 验证自定义存储不启用内置死锁重试。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ReceiveAsync_WhenStoreIsCustom_ShouldNotRetry()
    {
        await WithDatabaseAsync(async (connection, connectionString, consumer, stream) =>
        {
            var factory = new FaultingFactory(connectionString, getterFailures: 1);
            var store = new DbOrderedEventStore(OrderedEventDatabase.MySql);
            var customStore = new Mock<IOrderedEventStore>();
            customStore.Setup(value => value.EnqueueAsync(It.IsAny<DbTransaction>(), It.IsAny<string>(),
                    It.IsAny<OrderedEventStream>(), It.IsAny<OrderedEventMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns((DbTransaction transaction, string currentConsumer, OrderedEventStream currentStream,
                    OrderedEventMessage message, CancellationToken token) =>
                    store.EnqueueAsync(transaction, currentConsumer, currentStream, message, token));
            var coordinator = new OrderedEventCoordinator(factory, customStore.Object);

            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                coordinator.ReceiveAsync(consumer, stream, Message()));

            Assert.Same(factory.InjectedErrors[0], failure);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.Equal(1, factory.Begins);
            Assert.Equal(1, factory.Rollbacks);
            Assert.Equal(1, factory.Disposals);
            Assert.Equal(0, await CountInboxAsync(connection, stream));
        });
    }

    /// <summary>
    /// 创建真实内置存储协调器。
    /// </summary>
    private static OrderedEventCoordinator CreateCoordinator(IOrderedEventTransactionFactory factory) =>
        new(factory, new DbOrderedEventStore(OrderedEventDatabase.MySql));

    /// <summary>
    /// 创建本测试的待接收消息。
    /// </summary>
    private static OrderedEventMessage Message() => new("event-1", 1, "payload");

    /// <summary>
    /// 在专用 MySQL 数据库中运行随机流并清理自身记录。
    /// </summary>
    private static async Task WithDatabaseAsync(Func<MySqlConnection, string, string, OrderedEventStream, Task> action)
    {
        var options = new MySqlConnectionStringBuilder(IntegrationTestConnectionStringResolver.Resolve("MySql"))
        {
            Pooling = false
        };
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(options.ConnectionString, "MySql");
        await using var connection = new MySqlConnection(options.ConnectionString);
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql))
        {
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        var scope = Guid.NewGuid().ToString("N");
        var stream = new OrderedEventStream(scope, "receive-retry", "item");
        try
        {
            await action(connection, options.ConnectionString, "consumer-" + scope, stream);
        }
        finally
        {
            var key = new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(stream);
            foreach (var table in new[] { "bing_event_inbox", "bing_event_inbox_archive", "bing_event_cursor", "bing_event_sequence" })
            {
                using var cleanup = new MySqlCommand($"DELETE FROM {table} WHERE StreamKey = @stream", connection);
                cleanup.Parameters.AddWithValue("@stream", key);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// 读取随机业务流的已提交收件数量。
    /// </summary>
    private static async Task<long> CountInboxAsync(MySqlConnection connection, OrderedEventStream stream)
    {
        using var command = new MySqlCommand("SELECT COUNT(*) FROM bing_event_inbox WHERE StreamKey = @stream", connection);
        command.Parameters.AddWithValue("@stream", new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(stream));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 在真实 MySQL 事务中注入指定阶段的原生错误。
    /// </summary>
    private sealed class FaultingFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 原始测试连接字符串。
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// 事务获取阶段注入的失败次数。
        /// </summary>
        private readonly int _getterFailures;

        /// <summary>
        /// 事务获取阶段的 MySQL 错误号。
        /// </summary>
        private readonly int _getterFailureNumber;

        /// <summary>
        /// 提交阶段的 MySQL 错误号。
        /// </summary>
        private readonly int? _commitFailureNumber;

        /// <summary>
        /// 事务释放后的注入错误。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 回滚后要取消的调用令牌源。
        /// </summary>
        private readonly CancellationTokenSource _cancelAfterRollback;

        /// <summary>
        /// 初始化一个 <see cref="FaultingFactory" /> 类型的实例。
        /// </summary>
        internal FaultingFactory(string connectionString, int getterFailures = 0, int getterFailureNumber = 1213,
            int? commitFailureNumber = null, Exception disposeFailure = null,
            CancellationTokenSource cancelAfterRollback = null)
        {
            _connectionString = connectionString;
            _getterFailures = getterFailures;
            _getterFailureNumber = getterFailureNumber;
            _commitFailureNumber = commitFailureNumber;
            _disposeFailure = disposeFailure;
            _cancelAfterRollback = cancelAfterRollback;
        }

        /// <summary>
        /// 事务开始次数。
        /// </summary>
        internal int Begins { get; private set; }

        /// <summary>
        /// 提交调用次数。
        /// </summary>
        internal int Commits { get; private set; }

        /// <summary>
        /// 回滚调用次数。
        /// </summary>
        internal int Rollbacks { get; private set; }

        /// <summary>
        /// 释放调用次数。
        /// </summary>
        internal int Disposals { get; private set; }

        /// <summary>
        /// 由真实驱动生成的注入错误。
        /// </summary>
        internal List<MySqlException> InjectedErrors { get; } = new();

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                var transaction = await connection.BeginTransactionAsync(cancellationToken);
                Begins++;
                return new FaultingTransaction(connection, transaction, this, Begins <= _getterFailures);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// 执行真实 MySQL SIGNAL 并传播同一驱动异常。
        /// </summary>
        private MySqlException Signal(MySqlConnection connection, MySqlTransaction transaction, int number)
        {
            using var command = new MySqlCommand(
                $"SIGNAL SQLSTATE '40001' SET MYSQL_ERRNO={number}, MESSAGE_TEXT='injected_storage_error'",
                connection, transaction);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (MySqlException exception)
            {
                InjectedErrors.Add(exception);
                return exception;
            }
            throw new InvalidOperationException("MySQL SIGNAL 未产生预期错误。");
        }

        /// <summary>
        /// 包装真实 MySQL 连接和事务。
        /// </summary>
        private sealed class FaultingTransaction : IOrderedEventTransaction
        {
            /// <summary>
            /// 本次连接。
            /// </summary>
            private readonly MySqlConnection _connection;

            /// <summary>
            /// 本次事务。
            /// </summary>
            private readonly MySqlTransaction _transaction;

            /// <summary>
            /// 故障与计数配置。
            /// </summary>
            private readonly FaultingFactory _owner;

            /// <summary>
            /// 是否在读取事务时注入错误。
            /// </summary>
            private readonly bool _failOnGet;

            /// <summary>
            /// 初始化一个 <see cref="FaultingTransaction" /> 类型的实例。
            /// </summary>
            internal FaultingTransaction(MySqlConnection connection, MySqlTransaction transaction,
                FaultingFactory owner, bool failOnGet)
            {
                _connection = connection;
                _transaction = transaction;
                _owner = owner;
                _failOnGet = failOnGet;
            }

            /// <inheritdoc />
            public DbTransaction Transaction => _failOnGet
                ? throw _owner.Signal(_connection, _transaction, _owner._getterFailureNumber)
                : _transaction;

            /// <inheritdoc />
            public async Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _owner.Commits++;
                if (_owner._commitFailureNumber is int number)
                    throw _owner.Signal(_connection, _transaction, number);
                await _transaction.CommitAsync(cancellationToken);
            }

            /// <inheritdoc />
            public async Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                _owner.Rollbacks++;
                await _transaction.RollbackAsync(cancellationToken);
                _owner._cancelAfterRollback?.Cancel();
            }

            /// <inheritdoc />
            public async ValueTask DisposeAsync()
            {
                _owner.Disposals++;
                try
                {
                    await _transaction.DisposeAsync();
                }
                finally
                {
                    await _connection.DisposeAsync();
                }
                if (_owner._disposeFailure != null)
                    throw _owner._disposeFailure;
            }
        }
    }
}
