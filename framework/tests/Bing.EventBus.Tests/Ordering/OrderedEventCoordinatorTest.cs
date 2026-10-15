using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using Bing.Events.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件协调器的事务与失败恢复行为。
/// </summary>
public sealed class OrderedEventCoordinatorTest
{
    /// <summary>
    /// 验证消息头接收持久化后按实体序号处理。
    /// </summary>
    [Fact]
    public async Task ReceiveAsync_WhenMessagesArriveThreeOneTwo_ShouldPersistAndProcessInOrder()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", Headers("third", 3), "third");
        Assert.Equal(OrderedEventProcessingResult.Gap,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        await coordinator.ReceiveAsync("consumer", Headers("first", 1), "first");
        await coordinator.ReceiveAsync("consumer", Headers("second", 2), "second");
        await coordinator.ReceiveAsync("consumer", Headers("first", 1), "first");

        for (var sequence = 1; sequence <= 3; sequence++)
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));

        Assert.Equal(new[] { "first", "second", "third" }, await database.ReadBusinessIdsAsync());
        Assert.Equal(3, (await coordinator.GetStateAsync("consumer", stream)).LastSequence);
        Assert.Equal(OrderedEventProcessingResult.Empty,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
    }

    /// <summary>
    /// 验证协调器读取全局待处理业务流数量，而不是当前分页数量。
    /// </summary>
    [Fact]
    public async Task GetPendingStreamCountAsync_ShouldCountAllPendingStreams()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var first = OrderingSqliteFixture.Stream("first");
        var second = OrderingSqliteFixture.Stream("second");
        await coordinator.ReceiveAsync("consumer", first, new OrderedEventMessage("first", 1, "first"));
        await coordinator.ReceiveAsync("consumer", second, new OrderedEventMessage("second", 1, "second"));

        Assert.Equal(2, await coordinator.GetPendingStreamCountAsync("consumer"));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", first, OrderingSqliteFixture.WriteBusinessAsync));
        Assert.Equal(1, await coordinator.GetPendingStreamCountAsync("consumer"));
    }

    /// <summary>
    /// 验证协调器按事务清理已完成的旧收件记录。
    /// </summary>
    [Fact]
    public async Task DeleteProcessedBeforeAsync_ShouldDeleteCompletedRecordsOnly()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream,
            new OrderedEventMessage("event-1", 1, "payload"));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        await database.SetInboxCreatedAtAsync(DateTime.UtcNow.AddHours(-2));

        Assert.Equal(1, await coordinator.DeleteProcessedBeforeAsync("consumer",
            DateTime.UtcNow.AddHours(-1), 10));
        Assert.Equal(1, (await coordinator.GetStateAsync("consumer", stream)).LastSequence);
        Assert.Equal(0, (await coordinator.GetStateAsync("consumer", stream)).PendingCount);
    }

    /// <summary>
    /// 验证协调器按事务清理已过重放窗口的历史记录。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_ShouldDeleteExpiredHistoryOnly()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream("archive");
        await coordinator.ReceiveAsync("consumer", stream,
            new OrderedEventMessage("archive-1", 1, "payload"));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        await database.SetInboxCreatedAtAsync(DateTime.UtcNow.AddHours(-2));
        Assert.Equal(1, await coordinator.DeleteProcessedBeforeAsync("consumer",
            DateTime.UtcNow.AddHours(-1), 10));
        await database.SetArchiveArchivedAtAsync(DateTime.UtcNow.AddHours(-2));

        Assert.Equal(1, await coordinator.DeleteArchivedBeforeAsync("consumer",
            DateTime.UtcNow.AddHours(-1), 10));
        Assert.Equal(0, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 验证协调器收到取消后不会开启清理事务或删除历史。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenCanceled_ShouldNotBeginTransaction()
    {
        using var database = new OrderingSqliteFixture();
        var seed = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream("canceled-archive");
        await seed.ReceiveAsync("consumer", stream,
            new OrderedEventMessage("canceled-archive-1", 1, "payload"));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await seed.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        await database.SetInboxCreatedAtAsync(DateTime.UtcNow.AddHours(-2));
        Assert.Equal(1, await seed.DeleteProcessedBeforeAsync("consumer",
            DateTime.UtcNow.AddHours(-1), 10));
        await database.SetArchiveArchivedAtAsync(DateTime.UtcNow.AddHours(-2));

        var transactions = new CountingTransactionFactory(database.CreateFactory());
        var coordinator = new OrderedEventCoordinator(transactions, database.CreateStore());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.DeleteArchivedBeforeAsync(
            "consumer", DateTime.UtcNow.AddHours(-1), 10, cancellation.Token));

        Assert.Equal(0, transactions.Begins);
        Assert.Equal(1, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 验证业务失败回滚并记录阻塞，显式重试后继续处理。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenHandlerFails_ShouldRecordFailureAndRetry()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("second", 2, "two"));
        var original = new InvalidOperationException("handler failed");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, async (message, tx, token) =>
            {
                await OrderingSqliteFixture.WriteBusinessAsync(message, tx, token);
                throw original;
            }));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(true, failure.Data["Bing.OrderedEventBusinessRolledBack"]);
        Assert.Equal(true, failure.Data["Bing.OrderedEventFailureRecorded"]);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.True(state.Failed);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(0, state.LastSequence);
        Assert.Equal(nameof(InvalidOperationException), state.LastError);
        Assert.Equal(OrderedEventProcessingResult.Failed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));

        await coordinator.RetryAsync("consumer", stream);
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
        Assert.Equal(new[] { "first", "second" }, await database.ReadBusinessIdsAsync());
    }

    /// <summary>
    /// 人工恢复未配置授权器时应在开启事务前拒绝。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenAuthorizationIsMissing_ShouldRejectBeforeOpeningTransaction()
    {
        using var database = new OrderingSqliteFixture();
        var transactions = new CountingTransactionFactory(database.CreateFactory());
        var metrics = new BingOrderedEventMetrics("consumer");
        var coordinator = new OrderedEventCoordinator(transactions, database.CreateStore(),
            metrics: new[] { metrics });
        var stream = OrderingSqliteFixture.Stream();
        await SeedFailedEventAsync(coordinator, stream);
        var beginsBeforeRetry = transactions.Begins;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.RetryAsync("consumer", stream));

        Assert.Equal("有序事件恢复未配置授权器，已拒绝恢复操作。", exception.Message);
        Assert.Equal(beginsBeforeRetry, transactions.Begins);
        Assert.Equal(0, metrics.Snapshot.RetryCount);
        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.True(state.Failed);
        Assert.Equal(0, state.LastSequence);
    }

    /// <summary>
    /// 人工恢复未通过授权时不应开启数据库事务。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenRecoveryIsDenied_ShouldRejectBeforeOpeningTransaction()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new DenyingRecoveryAuthorizer();
        var metrics = new BingOrderedEventMetrics("consumer");
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            metrics: new[] { metrics }, authorizers: new[] { authorizer });
        var stream = OrderingSqliteFixture.Stream();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            coordinator.RetryAsync("consumer", stream));

        Assert.Equal("recovery denied", exception.Message);
        Assert.True(authorizer.Called);
        Assert.Equal(0, metrics.Snapshot.RetryCount);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        Assert.Equal(0, (await coordinator.GetStateAsync("consumer", stream)).LastSequence);
    }

    /// <summary>
    /// 验证恢复事务提交成功后只记录一次重试。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenCommitted_ShouldRecordRetryOnce()
    {
        using var database = new OrderingSqliteFixture();
        var metrics = new BingOrderedEventMetrics("consumer");
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            metrics: new[] { metrics }, authorizers: new[] { new AllowingRecoveryAuthorizer() });
        var stream = OrderingSqliteFixture.Stream();
        await SeedFailedEventAsync(coordinator, stream);

        await coordinator.RetryAsync("consumer", stream);

        Assert.Equal(1, metrics.Snapshot.RetryCount);
    }

    /// <summary>
    /// 验证提交结果未知时不记录恢复重试。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenCommitOutcomeIsUnknown_ShouldNotRecordRetry()
    {
        using var database = new OrderingSqliteFixture();
        var seed = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await SeedFailedEventAsync(seed, stream);
        var metrics = new BingOrderedEventMetrics("consumer");
        var original = new InvalidOperationException("commit uncertain");
        var coordinator = new OrderedEventCoordinator(new FaultingFactory(database.CreateFactory(),
            commitFailure: original), database.CreateStore(), metrics: new[] { metrics },
            authorizers: new[] { new AllowingRecoveryAuthorizer() });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.RetryAsync("consumer", stream));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        Assert.Equal(0, metrics.Snapshot.RetryCount);
    }

    /// <summary>
    /// 验证事务已提交但释放失败时仍记录恢复重试。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenDisposeFailsAfterCommit_ShouldRecordRetry()
    {
        using var database = new OrderingSqliteFixture();
        var seed = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await SeedFailedEventAsync(seed, stream);
        var metrics = new BingOrderedEventMetrics("consumer");
        var original = new InvalidOperationException("dispose failed");
        var coordinator = new OrderedEventCoordinator(new FaultingFactory(database.CreateFactory(),
            disposeFailure: original), database.CreateStore(), metrics: new[] { metrics },
            authorizers: new[] { new AllowingRecoveryAuthorizer() });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.RetryAsync("consumer", stream));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(1, metrics.Snapshot.RetryCount);
    }

    /// <summary>
    /// 授权器注册扩展使用作用域生命周期。
    /// </summary>
    [Fact]
    public void AddBingOrderedEventRecoveryAuthorization_ShouldRegisterScopedAuthorizer()
    {
        var services = new ServiceCollection();

        services.AddBingOrderedEventRecoveryAuthorization<DenyingRecoveryAuthorizer>();

        var descriptor = Assert.Single(services,
            item => item.ServiceType == typeof(IOrderedEventRecoveryAuthorizer));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(DenyingRecoveryAuthorizer), descriptor.ImplementationType);
    }

    /// <summary>
    /// 验证调用方取消后不写入且不将事件误标为业务失败。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenCanceled_ShouldNotRecordFailure()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync,
                cancellation.Token));

        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.False(state.Failed);
        Assert.Equal(0, state.Attempts);
        Assert.Equal(0, state.LastSequence);
        Assert.Empty(await database.ReadBusinessIdsAsync());
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));
    }

    /// <summary>
    /// 验证提交抛错时结果未知且业务与游标未被错误确认。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenCommitThrows_ShouldKeepOriginalAndUnknownOutcome()
    {
        using var database = new OrderingSqliteFixture();
        var seed = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await seed.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var original = new InvalidOperationException("commit uncertain");
        var coordinator = new OrderedEventCoordinator(new FaultingFactory(database.CreateFactory(),
            commitFailure: original), database.CreateStore());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        Assert.Empty(await database.ReadBusinessIdsAsync());
        Assert.Equal(0, (await seed.GetStateAsync("consumer", stream)).LastSequence);
        Assert.False((await seed.GetStateAsync("consumer", stream)).Failed);
    }

    /// <summary>
    /// 验证提交后释放失败保留原错及已提交结果。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenDisposeFailsAfterCommit_ShouldReportCommitted()
    {
        using var database = new OrderingSqliteFixture();
        var seed = CreateCoordinator(database);
        var stream = OrderingSqliteFixture.Stream();
        await seed.ReceiveAsync("consumer", stream, new OrderedEventMessage("first", 1, "one"));
        var original = new InvalidOperationException("dispose failed");
        var coordinator = new OrderedEventCoordinator(new FaultingFactory(database.CreateFactory(),
            disposeFailure: original), database.CreateStore());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, OrderingSqliteFixture.WriteBusinessAsync));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(new[] { "first" }, await database.ReadBusinessIdsAsync());
        Assert.Equal(1, (await seed.GetStateAsync("consumer", stream)).LastSequence);
    }

    /// <summary>
    /// 创建真实存储协调器。
    /// </summary>
    /// <param name="database">测试用的 SQLite 数据库夹具。</param>
    /// <returns>使用真实 SQLite 存储和事务工厂的协调器。</returns>
    private static OrderedEventCoordinator CreateCoordinator(OrderingSqliteFixture database) =>
        new(database.CreateFactory(), database.CreateStore(),
            authorizers: new[] { new AllowingRecoveryAuthorizer() });

    /// <summary>
    /// 创建一个已记录业务失败的待恢复事件。
    /// </summary>
    /// <param name="coordinator">用于创建和处理测试事件的协调器。</param>
    /// <param name="stream">待恢复的业务流。</param>
    private static async Task SeedFailedEventAsync(OrderedEventCoordinator coordinator, OrderedEventStream stream)
    {
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("failed", 1, "failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessNextAsync("consumer", stream, (message, transaction, token) =>
                Task.FromException(new InvalidOperationException("handler failed"))));
    }

    /// <summary>
    /// 创建与生产 CAP 格式一致的消息头。
    /// </summary>
    /// <param name="eventId">事件标识。</param>
    /// <param name="sequence">事件在业务流中的连续序号。</param>
    /// <returns>包含事件元数据的消息头集合。</returns>
    private static IReadOnlyDictionary<string, string> Headers(string eventId, long sequence) =>
        new Dictionary<string, string>
        {
            [ClusterEventHeaders.EventId] = eventId,
            [ClusterEventHeaders.TenantId] = "tenant",
            [ClusterEventHeaders.Stream] = "inventory",
            [ClusterEventHeaders.EntityKey] = "sku",
            [ClusterEventHeaders.Sequence] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    /// <summary>
    /// 拒绝人工恢复的测试授权器。
    /// </summary>
    private sealed class DenyingRecoveryAuthorizer : IOrderedEventRecoveryAuthorizer
    {
        /// <summary>
        /// 获取是否已执行授权检查。
        /// </summary>
        public bool Called { get; private set; }

        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            throw new UnauthorizedAccessException("recovery denied");
        }
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
    /// 记录恢复入口是否在授权成功前开启事务的测试工厂。
    /// </summary>
    private sealed class CountingTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 底层事务工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;

        /// <summary>
        /// 初始化一个 <see cref="CountingTransactionFactory" /> 类型的实例。
        /// </summary>
        /// <param name="inner">底层事务工厂。</param>
        internal CountingTransactionFactory(IOrderedEventTransactionFactory inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <summary>
        /// 获取已开启的事务数量。
        /// </summary>
        public int Begins { get; private set; }

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            Begins++;
            return await _inner.BeginAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 为真实事务注入指定生命周期故障。
    /// </summary>
    private sealed class FaultingFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 底层真实事务工厂。
        /// </summary>
        private readonly IOrderedEventTransactionFactory _inner;

        /// <summary>
        /// 提交故障。
        /// </summary>
        private readonly Exception _commitFailure;

        /// <summary>
        /// 释放故障。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 初始化一个 <see cref="FaultingFactory" /> 类型的实例。
        /// </summary>
        /// <param name="inner">底层真实事务工厂。</param>
        /// <param name="commitFailure">需要注入的提交异常；为空时不注入。</param>
        /// <param name="disposeFailure">需要注入的释放异常；为空时不注入。</param>
        internal FaultingFactory(IOrderedEventTransactionFactory inner, Exception commitFailure = null,
            Exception disposeFailure = null)
        {
            _inner = inner;
            _commitFailure = commitFailure;
            _disposeFailure = disposeFailure;
        }

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            new FaultingTransaction(await _inner.BeginAsync(cancellationToken), _commitFailure, _disposeFailure);
    }

    /// <summary>
    /// 在事务提交或释放处注入故障。
    /// </summary>
    private sealed class FaultingTransaction : IOrderedEventTransaction
    {
        /// <summary>
        /// 真实事务。
        /// </summary>
        private readonly IOrderedEventTransaction _inner;

        /// <summary>
        /// 提交故障。
        /// </summary>
        private readonly Exception _commitFailure;

        /// <summary>
        /// 释放故障。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 初始化一个 <see cref="FaultingTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="inner">底层真实事务。</param>
        /// <param name="commitFailure">需要注入的提交异常；为空时不注入。</param>
        /// <param name="disposeFailure">需要注入的释放异常；为空时不注入。</param>
        internal FaultingTransaction(IOrderedEventTransaction inner, Exception commitFailure, Exception disposeFailure)
        {
            _inner = inner;
            _commitFailure = commitFailure;
            _disposeFailure = disposeFailure;
        }

        /// <inheritdoc />
        public DbTransaction Transaction => _inner.Transaction;

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _commitFailure == null ? _inner.CommitAsync(cancellationToken) : Task.FromException(_commitFailure);

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) => _inner.RollbackAsync(cancellationToken);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            if (_disposeFailure != null)
                throw _disposeFailure;
        }
    }
}
