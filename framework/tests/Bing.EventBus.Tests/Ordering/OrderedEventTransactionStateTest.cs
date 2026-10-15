using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 使用真实 SQLite 事务验证保存点外状态清理与提交结果。
/// </summary>
public sealed class OrderedEventTransactionStateTest
{
    /// <summary>
    /// 业务保存点回滚后清理内存状态，再提交失败标记。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenBusinessFails_ShouldResetAfterDatabaseRollback()
    {
        using var database = new OrderingSqliteFixture();
        var factory = new StatefulFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(factory, database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var pending = new List<string>();
        factory.Reset = async transaction =>
        {
            using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM business_events";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            Assert.Single(pending);
            pending.Clear();
        };
        var original = new ApplicationException("业务失败。");

        var actual = await Assert.ThrowsAsync<ApplicationException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                pending.Add(message.EventId);
                throw original;
            }));

        Assert.Same(original, actual);
        Assert.Equal(1, factory.Resets);
        Assert.Empty(pending);
        Assert.Equal(TransactionOutcome.Committed, actual.GetTransactionOutcome());
        Assert.True((bool)actual.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.True(state.Failed);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(0, state.LastSequence);
    }

    /// <summary>
    /// 状态清理失败时回滚整笔事务，不提交失败标记或业务。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenResetFails_ShouldRollbackWholeTransactionAndKeepErrors()
    {
        using var database = new OrderingSqliteFixture();
        var factory = new StatefulFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(factory, database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var resetFailure = new InvalidOperationException("状态清理失败。");
        factory.Reset = _ => Task.FromException(resetFailure);
        var original = new ApplicationException("业务失败。");

        var actual = await Assert.ThrowsAsync<ApplicationException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                throw original;
            }));

        Assert.Same(original, actual);
        Assert.Same(resetFailure, actual.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.Equal(TransactionOutcome.RolledBack, actual.GetTransactionOutcome());
        Assert.False(actual.Data.Contains("Bing.OrderedEventFailureRecorded"));
        Assert.Equal(1, factory.Resets);
        Assert.Equal(1, factory.Rollbacks);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.False(state.Failed);
        Assert.Equal(0, state.Attempts);
        Assert.Equal(1, state.PendingCount);
    }

    /// <summary>
    /// 已确认提交后的发送失败保留 Committed，不再次调用物理回滚。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenCommitIsConfirmedThenFails_ShouldKeepCommittedOutcome()
    {
        using var database = new OrderingSqliteFixture();
        var factory = new StatefulFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(factory, database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var original = new ApplicationException("提交后的发送失败。");
        factory.AfterCommitFailure = original;

        var actual = await Assert.ThrowsAsync<ApplicationException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));

        Assert.Same(original, actual);
        Assert.Equal(TransactionOutcome.Committed, actual.GetTransactionOutcome());
        Assert.Equal(0, factory.Rollbacks);
        Assert.Equal(new[] { "first" }, await database.ReadBusinessIdsAsync());
        factory.AfterCommitFailure = null;
        Assert.Equal(1, (await coordinator.GetStateAsync("consumer", stream)).LastSequence);
    }

    /// <summary>
    /// 失败标记已提交后的发送失败同时保留业务原异常及标记提交事实。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenFailureMarkerCommitsThenFails_ShouldKeepBothErrorsAndMarker()
    {
        using var database = new OrderingSqliteFixture();
        var factory = new StatefulFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(factory, database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var original = new ApplicationException("业务失败。");
        var flushFailure = new InvalidOperationException("提交之后失败。");
        factory.AfterCommitFailure = flushFailure;

        var actual = await Assert.ThrowsAsync<ApplicationException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                throw original;
            }));

        Assert.Same(original, actual);
        Assert.Same(flushFailure, actual.Data["Bing.OrderedEventFailureRecordingException"]);
        Assert.True((bool)actual.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.True((bool)actual.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Equal(TransactionOutcome.Committed, actual.GetTransactionOutcome());
        Assert.Equal(0, factory.Rollbacks);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        factory.AfterCommitFailure = null;
        Assert.True((await coordinator.GetStateAsync("consumer", stream)).Failed);
    }

    /// <summary>
    /// 业务取消回滚整笔事务，不进入保存点失败标记与状态重置路径。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenCallerCancels_ShouldRollbackWithoutRecordingFailure()
    {
        using var database = new OrderingSqliteFixture();
        using var cancellation = new CancellationTokenSource();
        var factory = new StatefulFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(factory, database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, async (message, transaction, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, token);
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }, cancellation.Token));

        Assert.Equal(TransactionOutcome.RolledBack, actual.GetTransactionOutcome());
        Assert.Equal(0, factory.Resets);
        Assert.Equal(1, factory.Rollbacks);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        Assert.False((await coordinator.GetStateAsync("consumer", stream)).Failed);
    }

    /// <summary>
    /// 为真实 SQLite 事务增加可观测的适配器状态。
    /// </summary>
    private sealed class StatefulFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 每次创建真实 SQLite 事务的底层工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;

        /// <summary>
        /// 初始化一个 <see cref="StatefulFactory" /> 类型的实例。
        /// </summary>
        /// <param name="inner">底层 SQLite 事务工厂。</param>
        internal StatefulFactory(IOrderedEventTransactionFactory inner) => _inner = inner;

        /// <summary>
        /// 获取或设置实际保存点回滚后的状态重置动作。
        /// </summary>
        internal Func<DbTransaction, Task> Reset { get; set; }

        /// <summary>
        /// 获取或设置物理提交成功后需要传播的故障。
        /// </summary>
        internal Exception AfterCommitFailure { get; set; }

        /// <summary>
        /// 获取实际状态重置次数。
        /// </summary>
        internal int Resets { get; private set; }

        /// <summary>
        /// 获取显式物理回滚调用次数。
        /// </summary>
        internal int Rollbacks { get; private set; }

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            new StatefulTransaction(await _inner.BeginAsync(cancellationToken), this);

        /// <summary>
        /// 在真实 SQLite 事务外记录可确认结果与状态重置。
        /// </summary>
        private sealed class StatefulTransaction : IOrderedEventTransactionState
        {
            /// <summary>
            /// 拥有底层连接与数据库事务的真实事务。
            /// </summary>
            private readonly IOrderedEventTransaction _inner;

            /// <summary>
            /// 当前测试的故障与计数状态。
            /// </summary>
            private readonly StatefulFactory _factory;

            /// <summary>
            /// 初始化一个 <see cref="StatefulTransaction" /> 类型的实例。
            /// </summary>
            /// <param name="inner">底层 SQLite 事务。</param>
            /// <param name="factory">提供本轮观测的工厂。</param>
            internal StatefulTransaction(IOrderedEventTransaction inner, StatefulFactory factory)
            {
                _inner = inner;
                _factory = factory;
            }

            /// <inheritdoc />
            public void ActivateAmbient()
            {
            }

            /// <inheritdoc />
            public DbTransaction Transaction => _inner.Transaction;

            /// <inheritdoc />
            public TransactionOutcome Outcome { get; private set; } = TransactionOutcome.Unknown;

            /// <inheritdoc />
            public async Task CommitAsync(CancellationToken cancellationToken = default)
            {
                await _inner.CommitAsync(cancellationToken);
                Outcome = TransactionOutcome.Committed;
                if (_factory.AfterCommitFailure != null)
                    throw _factory.AfterCommitFailure;
            }

            /// <inheritdoc />
            public async Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                _factory.Rollbacks++;
                await _inner.RollbackAsync(cancellationToken);
                Outcome = TransactionOutcome.RolledBack;
            }

            /// <inheritdoc />
            public async Task ResetBusinessStateAsync(CancellationToken cancellationToken = default)
            {
                Assert.Equal(CancellationToken.None, cancellationToken);
                _factory.Resets++;
                if (_factory.Reset != null)
                    await _factory.Reset(Transaction);
            }

            /// <inheritdoc />
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }
    }
}
