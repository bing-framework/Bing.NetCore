using System.Data.Common;
using System.Reflection;
using Bing.Data.Events.Ordering;
using Bing.FreeSQL.Events;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using FreeSql;
using Moq;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证 FreeSQL 有序事件事务工厂的 SQLite 行为。
/// </summary>
public sealed class FreeSqlOrderedEventTransactionFactoryTest
{
    /// <summary>
    /// 验证真实 FreeSQL SQLite 事务回滚业务保存点并持久化失败标记。
    /// </summary>
    [Fact]
    public async Task Coordinator_WhenBusinessFails_ShouldRollbackSavepointAndBlockStream()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var authorizer = new Mock<IOrderedEventRecoveryAuthorizer>();
            authorizer.Setup(value => value.AuthorizeAsync(It.IsAny<string>(), It.IsAny<OrderedEventStream>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var coordinator = new OrderedEventCoordinator(factory,
                new DbOrderedEventStore(OrderedEventDatabase.Sqlite), authorizers: new[] { authorizer.Object });
            var stream = Stream();
            await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "payload"));
            var original = new InvalidOperationException("handler failed");
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                coordinator.ProcessNextAsync("consumer", stream, async (message, transaction, token) =>
                {
                    await WriteBusinessAsync(message, transaction, token);
                    throw original;
                }));
            Assert.Same(original, failure);
            Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
            Assert.Equal(true, failure.Data["Bing.OrderedEventFailureRecorded"]);
            await using (var read = await factory.BeginAsync())
            {
                Assert.Equal(0, await CountBusinessAsync(read.Transaction));
                await read.RollbackAsync();
            }
            var state = await coordinator.GetStateAsync("consumer", stream);
            Assert.True(state.Failed);
            Assert.Equal(0, state.LastSequence);
            await coordinator.RetryAsync("consumer", stream);
            authorizer.Verify(value => value.AuthorizeAsync("consumer", stream, It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await coordinator.ProcessNextAsync("consumer", stream, WriteBusinessAsync));
            await using var completed = await factory.BeginAsync();
            Assert.Equal(1, await CountBusinessAsync(completed.Transaction));
            await completed.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证自定义存储在旧驱动无原生保存点时拒绝执行业务。
    /// </summary>
    [Fact]
    public async Task Coordinator_WhenCustomStoreLacksNativeSavepoints_ShouldRollbackBeforeBusiness()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
            var ordinary = new OrderedEventCoordinator(factory, store);
            var stream = Stream();
            await ordinary.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "payload"));
            await using (var probe = await factory.BeginAsync())
            {
                Assert.False(probe.Transaction.SupportsSavepoints);
                await probe.RollbackAsync();
            }

            var customStore = new Mock<IOrderedEventStore>();
            customStore.Setup(value => value.ProcessNextAsync(It.IsAny<DbTransaction>(), It.IsAny<string>(),
                    It.IsAny<OrderedEventStream>(),
                    It.IsAny<Func<OrderedEventMessage, DbTransaction, CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((DbTransaction transaction, string consumer, OrderedEventStream currentStream,
                    Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
                    CancellationToken token) => store.ProcessNextAsync(transaction, consumer, currentStream,
                    handler, token));
            var transactions = new RollbackCountingFactory(factory);
            var coordinator = new OrderedEventCoordinator(transactions, customStore.Object);
            var invoked = false;

            var failure = await Assert.ThrowsAsync<NotSupportedException>(() =>
                coordinator.ProcessNextAsync("consumer", stream, async (message, transaction, token) =>
                {
                    invoked = true;
                    await WriteBusinessAsync(message, transaction, token);
                }));

            Assert.Contains("保存点", failure.Message);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.False(invoked);
            Assert.Equal(1, transactions.Begins);
            Assert.Equal(0, transactions.Commits);
            Assert.Equal(1, transactions.Rollbacks);
            Assert.Equal(1, transactions.Disposals);
            customStore.Verify(value => value.ProcessNextAsync(It.IsAny<DbTransaction>(), It.IsAny<string>(),
                It.IsAny<OrderedEventStream>(),
                It.IsAny<Func<OrderedEventMessage, DbTransaction, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            await using (var read = await factory.BeginAsync())
            {
                Assert.Equal(0, await CountBusinessAsync(read.Transaction));
                await read.RollbackAsync();
            }
            var state = await ordinary.GetStateAsync("consumer", stream);
            Assert.Equal(0, state.LastSequence);
            Assert.Equal(1, state.PendingCount);
            Assert.False(state.Failed);

            await using (var next = await factory.BeginAsync())
            {
                await WriteBusinessAsync(new OrderedEventMessage("still-open", 1, "payload"),
                    next.Transaction, CancellationToken.None);
                await next.CommitAsync();
            }
            await using var verify = await factory.BeginAsync();
            Assert.Equal(1, await CountBusinessAsync(verify.Transaction));
            await verify.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证事件、业务记录和游标一起提交且宿主 ORM 仍可使用。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenCommitted_ShouldPersistEventBusinessAndCursor()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
            var stream = Stream();

            await using (var transaction = await factory.BeginAsync())
            {
                Assert.NotNull(transaction.Transaction);
                Assert.Equal(1, await store.AllocateSequenceAsync(transaction.Transaction, stream));
                await store.EnqueueAsync(transaction.Transaction, "consumer", stream,
                    new OrderedEventMessage("event-1", 1, "payload"));
                Assert.Equal(OrderedEventProcessingResult.Processed,
                    await store.ProcessNextAsync(transaction.Transaction, "consumer", stream, WriteBusinessAsync));
                await transaction.CommitAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }

            await using (var read = await factory.BeginAsync())
            {
                Assert.Equal(1, await CountBusinessAsync(read.Transaction));
                var state = await store.GetStateAsync(read.Transaction, "consumer", stream);
                Assert.Equal(1, state.LastSequence);
                Assert.Equal(0, state.PendingCount);
                await read.RollbackAsync();
            }
            await using var another = await factory.BeginAsync();
            await another.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证显式回滚和未提交释放使业务及游标一同回退。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BeginAsync_WhenRolledBackOrDisposed_ShouldLeaveNoWrites(bool explicitRollback)
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
            var stream = Stream();

            await using (var transaction = await factory.BeginAsync())
            {
                Assert.Equal(1, await store.AllocateSequenceAsync(transaction.Transaction, stream));
                await store.EnqueueAsync(transaction.Transaction, "consumer", stream,
                    new OrderedEventMessage("event-1", 1, "payload"));
                Assert.Equal(OrderedEventProcessingResult.Processed,
                    await store.ProcessNextAsync(transaction.Transaction, "consumer", stream, WriteBusinessAsync));
                if (explicitRollback)
                    await transaction.RollbackAsync();
            }

            await using var retry = await factory.BeginAsync();
            Assert.Equal(0, await CountBusinessAsync(retry.Transaction));
            Assert.Equal(1, await store.AllocateSequenceAsync(retry.Transaction, stream));
            Assert.Equal(0, (await store.GetStateAsync(retry.Transaction, "consumer", stream)).LastSequence);
            await retry.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证预取消不创建事务或写入事件且宿主 ORM 仍可使用。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenAlreadyCanceled_ShouldNotWriteOrDisposeOrm()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.BeginAsync(cancellation.Token));

            await using var transaction = await factory.BeginAsync();
            Assert.Equal(0, await CountBusinessAsync(transaction.Transaction));
            Assert.Equal(0, await CountInboxAsync(transaction.Transaction));
            await transaction.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证回滚与释放同时失败仍保留两个原始错误且只清理一次。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenRollbackAndNativeDisposeFail_ShouldPreserveBothOnce()
    {
        var rollback = new InvalidOperationException("rollback failed");
        var dispose = new InvalidOperationException("dispose failed");
        var native = new Mock<global::FreeSql.IUnitOfWork>();
        native.Setup(value => value.Rollback()).Throws(rollback);
        native.Setup(value => value.Dispose()).Throws(dispose);
        var transaction = CreateScope(native.Object, new Mock<DbTransaction>().Object);

        var first = await Assert.ThrowsAsync<AggregateException>(() => transaction.DisposeAsync().AsTask());
        var second = await Assert.ThrowsAsync<AggregateException>(() => transaction.DisposeAsync().AsTask());

        Assert.Same(first, second);
        Assert.Equal(new Exception[] { rollback, dispose }, first.InnerExceptions);
        native.Verify(value => value.Rollback(), Times.Once);
        native.Verify(value => value.Dispose(), Times.Once);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.CommitAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.RollbackAsync());
    }

    /// <summary>
    /// 验证真实 SQLite 事务重复释放后拒绝操作且宿主 ORM 仍可使用。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ShouldRejectFurtherOperationsAndKeepOrm()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var transaction = await factory.BeginAsync();

            await transaction.DisposeAsync();
            await transaction.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.CommitAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.RollbackAsync());
            await using var next = await factory.BeginAsync();
            await WriteBusinessAsync(new OrderedEventMessage("still-open", 1, "payload"), next.Transaction,
                CancellationToken.None);
            await next.CommitAsync();
            await using var read = await factory.BeginAsync();
            Assert.Equal(1, await CountBusinessAsync(read.Transaction));
            await read.RollbackAsync();
        });
    }

    /// <summary>
    /// 保存点状态重置后不触发失败业务的实体变更通知。
    /// </summary>
    [Fact]
    public async Task ResetBusinessStateAsync_ShouldDiscardChangeReportWithoutCommitting()
    {
        var report = new global::FreeSql.DbContext.EntityChangeReport();
        report.Report.Add(new global::FreeSql.DbContext.EntityChangeReport.ChangeInfo());
        var notifications = 0;
        report.OnChange = _ => notifications++;
        var native = new Mock<global::FreeSql.IUnitOfWork>();
        native.SetupGet(value => value.EntityChangeReport).Returns(report);
        var transaction = CreateScope(native.Object, Mock.Of<DbTransaction>());
        var state = Assert.IsAssignableFrom<IOrderedEventTransactionState>(transaction);

        state.ActivateAmbient();
        await state.ResetBusinessStateAsync();

        Assert.Empty(report.Report);
        Assert.Equal(0, notifications);
        native.Verify(value => value.Commit(), Times.Never);
        native.Verify(value => value.Rollback(), Times.Never);
        Assert.Equal(TransactionOutcome.Unknown, state.Outcome);
        await state.RollbackAsync();
        Assert.Equal(TransactionOutcome.RolledBack, state.Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.ResetBusinessStateAsync());
        await state.DisposeAsync();
        native.Verify(value => value.Rollback(), Times.Once);
        native.Verify(value => value.Dispose(), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => state.ActivateAmbient());
    }

    /// <summary>
    /// 真实 SQLite 提交后保留结果并拒绝重新清理已结束事务。
    /// </summary>
    [Fact]
    public async Task TransactionState_WhenCommitted_ShouldKeepOutcomeAndOrmOwnership()
    {
        await WithDatabaseAsync(async orm =>
        {
            var factory = new FreeSqlOrderedEventTransactionFactory(orm);
            await CreateSchemaAsync(factory);
            var owner = await factory.BeginAsync();
            var state = Assert.IsAssignableFrom<IOrderedEventTransactionState>(owner);
            state.ActivateAmbient();
            await WriteBusinessAsync(new OrderedEventMessage("committed-state", 1, "payload"),
                state.Transaction, CancellationToken.None);
            await state.CommitAsync();
            Assert.Equal(TransactionOutcome.Committed, state.Outcome);
            await Assert.ThrowsAsync<InvalidOperationException>(() => state.ResetBusinessStateAsync());
            await owner.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => state.ResetBusinessStateAsync());
            await using var reader = await factory.BeginAsync();
            Assert.Equal(1, await CountBusinessAsync(reader.Transaction));
            await reader.RollbackAsync();
        });
    }

    /// <summary>
    /// 统计真实 FreeSQL 事务的外层提交和回滚。
    /// </summary>
    private sealed class RollbackCountingFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 真实事务工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;

        /// <summary>
        /// 初始化一个 <see cref="RollbackCountingFactory" /> 类型的实例。
        /// </summary>
        internal RollbackCountingFactory(IOrderedEventTransactionFactory inner) => _inner = inner;

        /// <summary>
        /// 事务开始次数。
        /// </summary>
        internal int Begins { get; private set; }

        /// <summary>
        /// 外层提交次数。
        /// </summary>
        internal int Commits { get; private set; }

        /// <summary>
        /// 外层回滚次数。
        /// </summary>
        internal int Rollbacks { get; private set; }

        /// <summary>
        /// 事务释放次数。
        /// </summary>
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _inner.BeginAsync(cancellationToken);
            Begins++;
            return new CountingTransaction(transaction, this);
        }

        /// <summary>
        /// 包装真实事务并记录外层操作。
        /// </summary>
        private sealed class CountingTransaction : IOrderedEventTransaction
        {
            /// <summary>
            /// 真实事务。
            /// </summary>
            private readonly IOrderedEventTransaction _inner;

            /// <summary>
            /// 所属计数器。
            /// </summary>
            private readonly RollbackCountingFactory _owner;

            /// <summary>
            /// 初始化一个 <see cref="CountingTransaction" /> 类型的实例。
            /// </summary>
            internal CountingTransaction(IOrderedEventTransaction inner, RollbackCountingFactory owner)
            {
                _inner = inner;
                _owner = owner;
            }

            /// <inheritdoc />
            public DbTransaction Transaction => _inner.Transaction;

            /// <inheritdoc />
            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _owner.Commits++;
                return _inner.CommitAsync(cancellationToken);
            }

            /// <inheritdoc />
            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                _owner.Rollbacks++;
                return _inner.RollbackAsync(cancellationToken);
            }

            /// <inheritdoc />
            public ValueTask DisposeAsync()
            {
                _owner.Disposals++;
                return _inner.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// 创建仅用于资源所有权测试的私有事务包装器。
    /// </summary>
    private static IOrderedEventTransaction CreateScope(global::FreeSql.IUnitOfWork native,
        DbTransaction transaction)
    {
        var type = typeof(FreeSqlOrderedEventTransactionFactory)
            .GetNestedType("TransactionScope", BindingFlags.NonPublic);
        Assert.NotNull(type);
        return Assert.IsAssignableFrom<IOrderedEventTransaction>(Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { native, transaction }, null));
    }

    /// <summary>
    /// 运行使用独立 SQLite 文件的测试。
    /// </summary>
    private static async Task WithDatabaseAsync(Func<IFreeSql, Task> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-ordered-freesql-{Guid.NewGuid():N}.db");
        try
        {
            using var orm = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={path}")
                .Build();
            await action(orm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 在测试数据库中执行生产有序事件建表脚本。
    /// </summary>
    private static async Task CreateSchemaAsync(FreeSqlOrderedEventTransactionFactory factory)
    {
        await using var transaction = await factory.BeginAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.Sqlite))
        {
            using var command = transaction.Transaction.Connection.CreateCommand();
            command.Transaction = transaction.Transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        using var business = transaction.Transaction.Connection.CreateCommand();
        business.Transaction = transaction.Transaction;
        business.CommandText = "CREATE TABLE business_events (EventId TEXT NOT NULL PRIMARY KEY)";
        await business.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 创建测试业务流。
    /// </summary>
    private static OrderedEventStream Stream() => new("tenant", "inventory", "sku");

    /// <summary>
    /// 使用处理器收到的事务写入业务记录。
    /// </summary>
    private static async Task WriteBusinessAsync(OrderedEventMessage message, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO business_events (EventId) VALUES (@id)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = message.EventId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取当前事务中的业务记录数。
    /// </summary>
    private static async Task<long> CountBusinessAsync(DbTransaction transaction) =>
        await CountAsync(transaction, "SELECT COUNT(*) FROM business_events");

    /// <summary>
    /// 读取当前事务中的收件记录数。
    /// </summary>
    private static async Task<long> CountInboxAsync(DbTransaction transaction) =>
        await CountAsync(transaction, "SELECT COUNT(*) FROM bing_event_inbox");

    /// <summary>
    /// 运行当前事务中的计数查询。
    /// </summary>
    private static async Task<long> CountAsync(DbTransaction transaction, string sql)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
