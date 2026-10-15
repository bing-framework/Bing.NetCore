using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件业务失败与失败标记的原子处理。
/// </summary>
public sealed class OrderedEventAtomicFailureTest
{
    /// <summary>
    /// 验证业务回滚后仅提交失败标记。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenBusinessFails_ShouldCommitOnlyFailureMarker()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var transactions = new RecordingFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(transactions, database.CreateStore());
        var original = new InvalidOperationException("business failed");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                throw original;
            }));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(true, failure.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Equal(true, failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.True(state.Failed);
        Assert.Equal(0, state.LastSequence);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(1, transactions.Commits);
        Assert.Equal(0, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
    }

    /// <summary>
    /// 验证执行中的取消回滚业务且不记录失败。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenCanceledDuringBusiness_ShouldRollbackWithoutFailureMarker()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var transactions = new RecordingFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(transactions, database.CreateStore());
        using var cancellation = new CancellationTokenSource();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, async (message, transaction, _) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, CancellationToken.None);
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }, cancellation.Token));

        Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
        Assert.Null(failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.False(state.Failed);
        Assert.Equal(0, state.Attempts);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(0, transactions.Commits);
        Assert.Equal(1, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
    }

    /// <summary>
    /// 验证失败标记提交前抛错时保留原始业务异常。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenFailureMarkerCommitThrowsBeforeNativeCommit_ShouldRemainPending()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var commitFailure = new InvalidOperationException("before native commit");
        var transactions = new RecordingFactory(database.CreateFactory(), commitBefore: commitFailure);
        var original = new InvalidOperationException("business failed");

        var failure = await ProcessFailingBusinessAsync(database, stream, transactions, original);

        Assert.Same(original, failure);
        Assert.Same(commitFailure, failure.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        Assert.Equal(true, failure.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Null(failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.False(state.Failed);
        Assert.Equal(0, state.Attempts);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(1, transactions.Commits);
        Assert.Equal(1, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
    }

    /// <summary>
    /// 验证失败标记提交后抛错时结果保持未知。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenFailureMarkerCommitThrowsAfterNativeCommit_ShouldRetainRecordedState()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var commitFailure = new InvalidOperationException("after native commit");
        var transactions = new RecordingFactory(database.CreateFactory(), commitAfter: commitFailure);
        var original = new InvalidOperationException("business failed");

        var failure = await ProcessFailingBusinessAsync(database, stream, transactions, original);

        Assert.Same(original, failure);
        Assert.Same(commitFailure, failure.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        Assert.Null(failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.True(state.Failed);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(1, transactions.Commits);
        Assert.Equal(1, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
    }

    /// <summary>
    /// 验证释放失败不遮蔽已提交失败标记的业务异常。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenDisposeThrowsAfterFailureMarkerCommit_ShouldRetainBusinessFailure()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var disposalFailure = new InvalidOperationException("dispose failed");
        var transactions = new RecordingFactory(database.CreateFactory(), disposeFailure: disposalFailure);
        var original = new InvalidOperationException("business failed");

        var failure = await ProcessFailingBusinessAsync(database, stream, transactions, original);

        Assert.Same(original, failure);
        Assert.Same(disposalFailure, failure.Data["Bing.TransactionCleanupException"]);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(true, failure.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Equal(true, failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        Assert.True((await ReadStateAsync(database, stream)).Failed);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(1, transactions.Commits);
        Assert.Equal(0, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
    }

    /// <summary>
    /// 验证业务、回滚和释放同时失败时保留原始业务异常及两类清理错误。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenBusinessRollbackAndDisposeFail_ShouldPreserveOriginalAndCleanupErrors()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var commitFailure = new InvalidOperationException("failure marker commit failed");
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var disposalFailure = new InvalidOperationException("dispose failed");
        var transactions = new RecordingFactory(database.CreateFactory(), commitBefore: commitFailure,
            rollbackFailure: rollbackFailure, disposeFailure: disposalFailure);
        var original = new InvalidOperationException("business failed");

        var failure = await ProcessFailingBusinessAsync(database, stream, transactions, original);

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        Assert.Same(commitFailure, failure.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.Same(rollbackFailure, failure.Data["Bing.TransactionRollbackException"]);
        Assert.Same(disposalFailure, failure.Data["Bing.TransactionCleanupException"]);
        Assert.Equal(1, transactions.Begins);
        Assert.Equal(1, transactions.Commits);
        Assert.Equal(1, transactions.Rollbacks);
        Assert.Equal(1, transactions.Disposals);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.False(state.Failed);
        Assert.Equal(1, state.PendingCount);
    }

    /// <summary>
    /// 验证重复使用业务异常时清除上一轮失败标记诊断。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenSameExceptionIsReused_ShouldReportOnlyCurrentFailureMarkerOutcome()
    {
        using var database = new OrderingSqliteFixture();
        var stream = await SeedAsync(database);
        var original = new InvalidOperationException("reused business failure");
        var firstTransactions = new RecordingFactory(database.CreateFactory());

        var first = await ProcessFailingBusinessAsync(database, stream, firstTransactions, original);
        Assert.Same(original, first);
        Assert.Equal(TransactionOutcome.Committed, first.GetTransactionOutcome());
        Assert.Equal(true, first.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.True((await ReadStateAsync(database, stream)).Failed);

        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            authorizers: new[] { new AllowingRecoveryAuthorizer() });
        await coordinator.RetryAsync("consumer", stream);
        var commitFailure = new InvalidOperationException("current marker commit failed");
        var secondTransactions = new RecordingFactory(database.CreateFactory(), commitBefore: commitFailure);

        var second = await ProcessFailingBusinessAsync(database, stream, secondTransactions, original);

        Assert.Same(original, second);
        Assert.Equal(TransactionOutcome.Unknown, second.GetTransactionOutcome());
        Assert.Null(second.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Equal(true, second.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Same(commitFailure, second.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await ReadStateAsync(database, stream);
        Assert.False(state.Failed);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal(1, secondTransactions.Begins);
        Assert.Equal(1, secondTransactions.Commits);
        Assert.Equal(1, secondTransactions.Rollbacks);
        Assert.Equal(1, secondTransactions.Disposals);
    }

    /// <summary>
    /// 向真实存储写入一条待处理事件。
    /// </summary>
    private static async Task<OrderedEventStream> SeedAsync(OrderingSqliteFixture database)
    {
        var stream = OrderingSqliteFixture.Stream();
        await database.InTransactionAsync(transaction => database.CreateStore().EnqueueAsync(
            transaction, "consumer", stream, new OrderedEventMessage("event-1", 1, "payload")));
        return stream;
    }

    /// <summary>
    /// 读取已提交的业务流状态。
    /// </summary>
    private static Task<OrderedEventStreamState> ReadStateAsync(OrderingSqliteFixture database,
        OrderedEventStream stream) =>
        new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore()).GetStateAsync("consumer", stream);

    /// <summary>
    /// 写入业务后触发原始失败。
    /// </summary>
    private static Task<InvalidOperationException> ProcessFailingBusinessAsync(OrderingSqliteFixture database,
        OrderedEventStream stream, RecordingFactory transactions, InvalidOperationException original)
    {
        var coordinator = new OrderedEventCoordinator(transactions, database.CreateStore());
        return Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                throw original;
            }));
    }

    /// <summary>
    /// 允许人工恢复的测试授权器。
    /// </summary>
    private sealed class AllowingRecoveryAuthorizer : IOrderedEventRecoveryAuthorizer
    {
        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 统计外层事务并注入提交、回滚与释放故障。
    /// </summary>
    private sealed class RecordingFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 真实 SQLite 事务工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;

        /// <summary>
        /// 原生提交前故障。
        /// </summary>
        private readonly Exception _commitBefore;

        /// <summary>
        /// 原生提交后故障。
        /// </summary>
        private readonly Exception _commitAfter;

        /// <summary>
        /// 原生回滚故障。
        /// </summary>
        private readonly Exception _rollbackFailure;

        /// <summary>
        /// 释放故障。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 初始化一个 <see cref="RecordingFactory" /> 类型的实例。
        /// </summary>
        internal RecordingFactory(IOrderedEventTransactionFactory inner, Exception commitBefore = null,
            Exception commitAfter = null, Exception rollbackFailure = null, Exception disposeFailure = null)
        {
            _inner = inner;
            _commitBefore = commitBefore;
            _commitAfter = commitAfter;
            _rollbackFailure = rollbackFailure;
            _disposeFailure = disposeFailure;
        }

        /// <summary>
        /// 已开始的事务次数。
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

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _inner.BeginAsync(cancellationToken);
            Begins++;
            return new RecordingTransaction(transaction, this);
        }

        /// <summary>
        /// 包装真实事务并记录生命周期调用。
        /// </summary>
        private sealed class RecordingTransaction : IOrderedEventTransaction
        {
            /// <summary>
            /// 真实事务。
            /// </summary>
            private readonly IOrderedEventTransaction _inner;

            /// <summary>
            /// 统计及故障配置。
            /// </summary>
            private readonly RecordingFactory _owner;

            /// <summary>
            /// 初始化一个 <see cref="RecordingTransaction" /> 类型的实例。
            /// </summary>
            internal RecordingTransaction(IOrderedEventTransaction inner, RecordingFactory owner)
            {
                _inner = inner;
                _owner = owner;
            }

            /// <inheritdoc />
            public DbTransaction Transaction => _inner.Transaction;

            /// <inheritdoc />
            public async Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _owner.Commits++;
                if (_owner._commitBefore != null)
                    throw _owner._commitBefore;
                await _inner.CommitAsync(cancellationToken);
                if (_owner._commitAfter != null)
                    throw _owner._commitAfter;
            }

            /// <inheritdoc />
            public async Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                _owner.Rollbacks++;
                if (_owner._rollbackFailure != null)
                    throw _owner._rollbackFailure;
                await _inner.RollbackAsync(cancellationToken);
            }

            /// <inheritdoc />
            public async ValueTask DisposeAsync()
            {
                _owner.Disposals++;
                await _inner.DisposeAsync();
                if (_owner._disposeFailure != null)
                    throw _owner._disposeFailure;
            }
        }
    }
}
