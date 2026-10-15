using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件恢复服务的授权与关键审计行为。
/// </summary>
public sealed class OrderedEventRecoveryServiceTest
{
    /// <summary>
    /// 恢复审计记录拒绝带边界空白的消费者名称。
    /// </summary>
    [Theory]
    [InlineData(" consumer")]
    [InlineData("consumer ")]
    public void RecoveryAuditEntry_WhenConsumerHasBoundaryWhitespace_ShouldReject(string consumer)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new OrderedEventRecoveryAuditEntry("Retry", consumer, OrderingSqliteFixture.Stream(), true));

        Assert.Equal("consumer", exception.ParamName);
    }

    /// <summary>
    /// 状态查询应在开启业务事务前执行授权。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenAuthorizationFails_ShouldNotQueryState()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new DenyingAuthorizer();
        var service = CreateService(database, authorizer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetStateAsync("consumer", OrderingSqliteFixture.Stream()));

        Assert.True(authorizer.Called);
    }

    /// <summary>
    /// 未配置授权器时拒绝状态查询。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenAuthorizationIsMissing_ShouldReject()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var service = new OrderedEventRecoveryService(coordinator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetStateAsync("consumer", OrderingSqliteFixture.Stream()));

        Assert.Contains("未配置授权器", exception.Message);
    }

    /// <summary>
    /// 未配置授权器时拒绝人工重试。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenAuthorizationIsMissing_ShouldReject()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var service = new OrderedEventRecoveryService(coordinator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetryAsync("consumer", OrderingSqliteFixture.Stream()));

        Assert.Contains("未配置授权器", exception.Message);
    }

    /// <summary>
    /// 授权成功但未配置审计记录器时应在读取状态前拒绝。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenAuditIsMissing_ShouldRejectAfterAuthorization()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new AllowingAuthorizer();
        var service = CreateServiceWithoutAudit(database, authorizer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetStateAsync("consumer", OrderingSqliteFixture.Stream()));

        Assert.Equal("有序事件恢复未配置审计记录器，已拒绝操作。", exception.Message);
        Assert.Equal(1, authorizer.Calls);
    }

    /// <summary>
    /// 授权成功但未配置审计记录器时应在恢复事务前拒绝。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenAuditIsMissing_ShouldRejectBeforeRetry()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new AllowingAuthorizer();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            authorizers: new[] { authorizer });
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("failed", 1, "payload"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ProcessNextAsync("consumer", stream,
            (message, transaction, token) => throw new InvalidOperationException("business")));
        var service = new OrderedEventRecoveryService(coordinator, new[] { authorizer });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetryAsync("consumer", stream));

        Assert.Equal("有序事件恢复未配置审计记录器，已拒绝操作。", exception.Message);
        Assert.True((await coordinator.GetStateAsync("consumer", stream)).Failed);
    }

    /// <summary>
    /// 人工重试应沿用协调器授权并写入成功审计。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenCommitted_ShouldWriteSuccessfulAudit()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new AllowingAuthorizer();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            authorizers: new[] { authorizer });
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("failed", 1, "payload"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ProcessNextAsync("consumer", stream,
            (message, transaction, token) => throw new InvalidOperationException("business")));

        var audit = new RecordingAudit();
        var service = new OrderedEventRecoveryService(coordinator, new[] { authorizer }, new[] { audit });
        await service.RetryAsync("consumer", stream);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("Retry", entry.Operation);
        Assert.True(entry.Succeeded);
        Assert.Same(stream, entry.Stream);
        Assert.Equal(1, authorizer.Calls);
    }

    /// <summary>
    /// 恢复已经提交时审计失败不得伪装成事务回滚。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenAuditFailsAfterCommit_ShouldPreserveCommittedOutcome()
    {
        using var database = new OrderingSqliteFixture();
        var authorizer = new AllowingAuthorizer();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(),
            authorizers: new[] { authorizer });
        var stream = OrderingSqliteFixture.Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("failed", 1, "payload"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ProcessNextAsync("consumer", stream,
            (message, transaction, token) => throw new InvalidOperationException("business")));

        var service = new OrderedEventRecoveryService(coordinator, new[] { authorizer },
            new[] { new ThrowingAudit() });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetryAsync("consumer", stream));

        Assert.Equal(Bing.Data.Transaction.TransactionOutcome.Committed, exception.GetTransactionOutcome());
        Assert.True((bool)exception.Data["Bing.OrderedEventRecoveryAuditCommitted"]);
        Assert.False((await coordinator.GetStateAsync("consumer", stream)).Failed);
    }

    /// <summary>
    /// 创建使用 SQLite 的恢复服务。
    /// </summary>
    private static OrderedEventRecoveryService CreateService(OrderingSqliteFixture database,
        IOrderedEventRecoveryAuthorizer authorizer) => new(
        new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(), authorizers: new[] { authorizer }),
        new[] { authorizer });

    /// <summary>
    /// 创建没有审计记录器的恢复服务。
    /// </summary>
    private static OrderedEventRecoveryService CreateServiceWithoutAudit(OrderingSqliteFixture database,
        IOrderedEventRecoveryAuthorizer authorizer) => new(
        new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore(), authorizers: new[] { authorizer }),
        new[] { authorizer });

    /// <summary>
    /// 始终拒绝有序事件恢复操作的测试授权器。
    /// </summary>
    private sealed class DenyingAuthorizer : IOrderedEventRecoveryAuthorizer
    {
        /// <summary>
        /// 获取授权器是否已被调用。
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
    /// 始终允许有序事件恢复操作的测试授权器。
    /// </summary>
    private sealed class AllowingAuthorizer : IOrderedEventRecoveryAuthorizer
    {
        /// <summary>
        /// 获取授权器调用次数。
        /// </summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录有序事件恢复审计项的测试实现。
    /// </summary>
    private sealed class RecordingAudit : IOrderedEventRecoveryAudit
    {
        /// <summary>
        /// 获取已记录的恢复审计项。
        /// </summary>
        public List<OrderedEventRecoveryAuditEntry> Entries { get; } = new();

        /// <inheritdoc />
        public Task RecordAsync(OrderedEventRecoveryAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 在记录审计项时抛出异常的测试实现。
    /// </summary>
    private sealed class ThrowingAudit : IOrderedEventRecoveryAudit
    {
        /// <inheritdoc />
        public Task RecordAsync(OrderedEventRecoveryAuditEntry entry,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("audit failed"));
    }
}
