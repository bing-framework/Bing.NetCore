using System.Data.Common;
using System.Reflection;
using Bing.Data.Inventory;
using Bing.Data.Events.Ordering;
using Bing.EntityFrameworkCore.Inventory;
using Bing.EntityFrameworkCore.Events;
using Bing.Data.Transaction;
using Bing.Events.Ordering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Events;

/// <summary>
/// 验证 EFCore 有序事件事务工厂的 SQLite 行为。
/// </summary>
public sealed class EfCoreOrderedEventTransactionFactoryTest
{
    /// <summary>
    /// 验证库存适配复用 EFCore 当前事务并提交条件扣减。
    /// </summary>
    [Fact]
    public async Task TryDeductInventoryAsync_ShouldReuseCurrentEfCoreTransaction()
    {
        await using var connection = await CreateInventoryConnectionAsync();
        using var context = CreateContext(connection);
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);
        var request = new InventoryDeductionRequest("tenant", "warehouse", "sku", 3, "ef-request");

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var result = await context.TryDeductInventoryAsync(store, request);
            Assert.True(result.Applied);
            Assert.Equal(7, result.RemainingQuantity);
            await transaction.CommitAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Quantity FROM bing_inventory_stock " +
                              "WHERE TenantId = 'tenant' AND WarehouseId = 'warehouse' AND Sku = 'sku'";
        Assert.Equal(7L, (long)await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 没有活动事务时拒绝执行库存扣减。
    /// </summary>
    [Fact]
    public async Task TryDeductInventoryAsync_WhenNoCurrentTransaction_ShouldReject()
    {
        await using var connection = await CreateInventoryConnectionAsync();
        using var context = CreateContext(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.TryDeductInventoryAsync(
            new DbInventoryStore(InventoryDatabase.Sqlite),
            new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "ef-no-transaction")));
    }

    /// <summary>
    /// 验证真实 EFCore 事务回滚业务保存点并持久化失败标记。
    /// </summary>
    [Fact]
    public async Task Coordinator_WhenBusinessFails_ShouldRollbackSavepointAndBlockStream()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var coordinator = new OrderedEventCoordinator(new EfCoreOrderedEventTransactionFactory<TestContext>(context),
            new DbOrderedEventStore(OrderedEventDatabase.Sqlite),
            authorizers: new[] { new AllowingRecoveryAuthorizer() });
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
        Assert.Equal(0, await CountBusinessAsync(connection));
        var state = await coordinator.GetStateAsync("consumer", stream);
        Assert.True(state.Failed);
        Assert.Equal(0, state.LastSequence);
        await coordinator.RetryAsync("consumer", stream);
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync("consumer", stream, WriteBusinessAsync));
        Assert.Equal(1, await CountBusinessAsync(connection));
    }

    /// <summary>
    /// 验证事件、业务记录和游标在同一事务中提交且上下文仍可使用。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenCommitted_ShouldPersistEventBusinessAndCursor()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
        var stream = Stream();

        await using (var transaction = await factory.BeginAsync())
        {
            Assert.IsType<SqliteTransaction>(transaction.Transaction);
            Assert.Equal(1, await store.AllocateSequenceAsync(transaction.Transaction, stream));
            await store.EnqueueAsync(transaction.Transaction, "consumer", stream,
                new OrderedEventMessage("event-1", 1, "payload"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(transaction.Transaction, "consumer", stream, WriteBusinessAsync));
            await transaction.CommitAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
        }

        Assert.Equal(1, await CountBusinessAsync(connection));
        await using (var transaction = await factory.BeginAsync())
        {
            var state = await store.GetStateAsync(transaction.Transaction, "consumer", stream);
            Assert.Equal(1, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
            await transaction.RollbackAsync();
        }
        Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
    }

    /// <summary>
    /// 验证显式回滚和未提交释放不保留业务或事件状态。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BeginAsync_WhenRolledBackOrDisposed_ShouldLeaveNoWrites(bool explicitRollback)
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
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

        Assert.Equal(0, await CountBusinessAsync(connection));
        await using (var retry = await factory.BeginAsync())
        {
            Assert.Equal(1, await store.AllocateSequenceAsync(retry.Transaction, stream));
            Assert.Equal(0, (await store.GetStateAsync(retry.Transaction, "consumer", stream)).LastSequence);
            await retry.RollbackAsync();
        }
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// 验证工厂拒绝接管外部事务且不结束它。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenExternalTransactionExists_ShouldLeaveItOwnedByCaller()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        await using var external = await context.Database.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.BeginAsync());

        Assert.Same(external, context.Database.CurrentTransaction);
        await InsertBusinessAsync(external.GetDbTransaction(), "external", CancellationToken.None);
        await external.CommitAsync();
        Assert.Equal(1, await CountBusinessAsync(connection));
    }

    /// <summary>
    /// 验证预取消不创建事务或事件记录。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenAlreadyCanceled_ShouldNotWriteOrDisposeContext()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.BeginAsync(cancellation.Token));

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(0, await CountBusinessAsync(connection));
        await using var transaction = await factory.BeginAsync();
        await transaction.RollbackAsync();
    }

    /// <summary>
    /// 验证重复异步释放共同等待唯一的原生清理。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ConcurrentCalls_ShouldAwaitOneNativeCleanup()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new Mock<IDbContextTransaction>();
        native.Setup(value => value.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        native.Setup(value => value.DisposeAsync())
            .Returns(() => new ValueTask(release.Task));
        var transaction = CreateScope(native.Object);

        var first = transaction.DisposeAsync().AsTask();
        var second = transaction.DisposeAsync().AsTask();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        native.Verify(value => value.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        native.Verify(value => value.DisposeAsync(), Times.Once);
        release.SetResult(true);
        await Task.WhenAll(first, second);
        await transaction.DisposeAsync();
        native.Verify(value => value.DisposeAsync(), Times.Once);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.CommitAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.RollbackAsync());
    }

    /// <summary>
    /// 验证回滚和释放同时失败仍对每个调用方报告相同错误。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenRollbackAndNativeDisposeFail_ShouldPreserveBothOnce()
    {
        var rollback = new InvalidOperationException("rollback failed");
        var dispose = new InvalidOperationException("dispose failed");
        var native = new Mock<IDbContextTransaction>();
        native.Setup(value => value.RollbackAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(rollback);
        native.Setup(value => value.DisposeAsync())
            .Returns(new ValueTask(Task.FromException(dispose)));
        var transaction = CreateScope(native.Object);

        var first = await Assert.ThrowsAsync<AggregateException>(() => transaction.DisposeAsync().AsTask());
        var second = await Assert.ThrowsAsync<AggregateException>(() => transaction.DisposeAsync().AsTask());

        Assert.Same(first, second);
        Assert.Equal(new Exception[] { rollback, dispose }, first.InnerExceptions);
        native.Verify(value => value.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        native.Verify(value => value.DisposeAsync(), Times.Once);
    }

    /// <summary>
    /// 验证真实 SQLite 事务关闭后拒绝操作且不释放宿主上下文。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ShouldRejectFurtherOperationsAndKeepContext()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        var transaction = await factory.BeginAsync();

        await transaction.DisposeAsync();
        await transaction.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.CommitAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.RollbackAsync());
        await using var next = await factory.BeginAsync();
        await InsertBusinessAsync(next.Transaction, "still-open", CancellationToken.None);
        await next.CommitAsync();
        Assert.Equal(1, await CountBusinessAsync(connection));
    }

    /// <summary>
    /// 保存点回滚后清除已保存及未保存实体，避免后续保存重新写入失败业务。
    /// </summary>
    [Fact]
    public async Task Coordinator_WhenTrackedBusinessFails_ShouldClearAllFailedEntities()
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        var coordinator = new OrderedEventCoordinator(factory, new DbOrderedEventStore(OrderedEventDatabase.Sqlite),
            authorizers: new[] { new AllowingRecoveryAuthorizer() });
        var stream = Stream();
        await coordinator.ReceiveAsync("consumer", stream, new OrderedEventMessage("tracked", 1, "payload"));
        var original = new InvalidOperationException("跟踪业务失败。");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ProcessNextAsync(
            "consumer", stream, async (message, transaction, token) =>
            {
                context.Add(new BusinessRow { EventId = message.EventId });
                await context.SaveChangesAsync(token);
                context.Add(new BusinessRow { EventId = "pending" });
                Assert.Equal(2, context.ChangeTracker.Entries().Count());
                throw original;
            }));

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await context.SaveChangesAsync());
        Assert.Equal(0, await CountBusinessAsync(connection));
        Assert.True((await coordinator.GetStateAsync("consumer", stream)).Failed);
        await coordinator.RetryAsync("consumer", stream);
        await coordinator.ProcessNextAsync("consumer", stream, async (message, _, token) =>
        {
            context.Add(new BusinessRow { EventId = message.EventId });
            await context.SaveChangesAsync(token);
        });
        Assert.Equal(1, await CountBusinessAsync(connection));
    }

    /// <summary>
    /// 实际物理提交与回滚后提供准确结果，不允许关闭后重置状态。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransactionState_WhenFinished_ShouldExposeOutcomeAndRejectReset(bool commit)
    {
        await using var connection = await CreateConnectionAsync();
        using var context = CreateContext(connection);
        var factory = new EfCoreOrderedEventTransactionFactory<TestContext>(context);
        var owner = await factory.BeginAsync();
        var state = Assert.IsAssignableFrom<IOrderedEventTransactionState>(owner);
        state.ActivateAmbient();
        Assert.Equal(TransactionOutcome.Unknown, state.Outcome);
        if (commit)
            await state.CommitAsync();
        else
            await state.RollbackAsync();
        Assert.Equal(commit ? TransactionOutcome.Committed : TransactionOutcome.RolledBack, state.Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.ResetBusinessStateAsync());
        await owner.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => state.ResetBusinessStateAsync());
        Assert.Throws<ObjectDisposedException>(() => state.ActivateAmbient());
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// 创建仅用于资源所有权测试的私有事务包装器。
    /// </summary>
    private static IOrderedEventTransaction CreateScope(IDbContextTransaction native)
    {
        var type = typeof(EfCoreOrderedEventTransactionFactory<TestContext>)
            .GetNestedType("TransactionScope", BindingFlags.NonPublic);
        Assert.NotNull(type);
        if (type.ContainsGenericParameters)
            type = type.MakeGenericType(typeof(TestContext));
        return Assert.IsAssignableFrom<IOrderedEventTransaction>(Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { native }, null));
    }

    /// <summary>
    /// 创建 SQLite 测试连接与表。
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.Sqlite))
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        using var business = connection.CreateCommand();
        business.CommandText = "CREATE TABLE business_events (EventId TEXT NOT NULL PRIMARY KEY)";
        await business.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// 创建带库存表的 SQLite 测试连接。
    /// </summary>
    private static async Task<SqliteConnection> CreateInventoryConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        foreach (var sql in InventorySchema.GetCreateStatements(InventoryDatabase.Sqlite))
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        using var seed = connection.CreateCommand();
        seed.CommandText = "INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                           "VALUES ('tenant', 'warehouse', 'sku', 10)";
        await seed.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// 创建使用宿主连接的数据库上下文。
    /// </summary>
    private static TestContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TestContext>().UseSqlite(connection).Options);

    /// <summary>
    /// 创建测试业务流。
    /// </summary>
    private static OrderedEventStream Stream() => new("tenant", "inventory", "sku");

    /// <summary>
    /// 使用处理器收到的事务写入业务记录。
    /// </summary>
    private static Task WriteBusinessAsync(OrderedEventMessage message, DbTransaction transaction,
        CancellationToken cancellationToken) => InsertBusinessAsync(transaction, message.EventId, cancellationToken);

    /// <summary>
    /// 在指定事务内写入业务记录。
    /// </summary>
    private static async Task InsertBusinessAsync(DbTransaction transaction, string eventId,
        CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO business_events (EventId) VALUES (@id)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = eventId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取已提交业务记录数量。
    /// </summary>
    private static async Task<long> CountBusinessAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM business_events";
        return (long)await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// 仅供测试通过人工恢复授权检查。
    /// </summary>
    private sealed class AllowingRecoveryAuthorizer : IOrderedEventRecoveryAuthorizer
    {
        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 映射失败保存点中的业务记录。
    /// </summary>
    private sealed class BusinessRow
    {
        /// <summary>
        /// 获取或设置业务记录标识。
        /// </summary>
        public string EventId { get; set; }
    }

    /// <summary>
    /// 测试用 EFCore 数据库上下文。
    /// </summary>
    private sealed class TestContext : DbContext
    {
        /// <summary>
        /// 初始化一个 <see cref="TestContext" /> 类型的实例。
        /// </summary>
        public TestContext(DbContextOptions<TestContext> options) : base(options)
        {
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BusinessRow>().ToTable("business_events").HasKey(row => row.EventId);
        }
    }
}
