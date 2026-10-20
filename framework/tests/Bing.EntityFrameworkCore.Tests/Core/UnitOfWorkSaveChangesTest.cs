using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities.Events;
using Bing.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// <see cref="UnitOfWorkBase"/> 保存行为单元测试。
/// </summary>
public class UnitOfWorkSaveChangesTest
{
    /// <summary>
    /// 测试目的：默认 EF Core 配置不得启用敏感数据日志，避免参数值进入生产日志。
    /// </summary>
    [Fact]
    public void ConfiguringLog_WhenDefaultOptionsAreUsed_ShouldDisableSensitiveDataLogging()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);

        // Act
        var options = unitOfWork.GetService<IDbContextOptions>();
        var coreOptions = options.FindExtension<CoreOptionsExtension>();

        // Assert
        Assert.NotNull(coreOptions);
        Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
    }

    /// <summary>
    /// 测试目的：分页总数查询在调用前已取消时必须观察同一取消令牌，不能继续访问数据库。
    /// </summary>
    [Fact]
    public async Task PageAsync_WhenCountTokenIsCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        var pager = new Pager(1, 10);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.Samples.PageAsync(
            pager, cancellationTokenSource.Token));
        Assert.True(string.IsNullOrWhiteSpace(pager.Order));
        Assert.Equal(0, pager.TotalCount);
        Assert.False(pager.IsTotalCountKnown);
    }

    /// <summary>
    /// 测试目的：预取消的异步保存不得进入保存前拦截或创建事务动作。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancellationRequested_ShouldNotInvokeSaveChangesBefore()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Samples.Add(new SaveSample { Name = "cancelled" });
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.SaveChangesAsync(cancellationTokenSource.Token));
        Assert.Equal(0, unitOfWork.SaveChangesBeforeCount);
    }

    /// <summary>
    /// 测试目的：事务动作成功后工作单元只能借用 DbContext 连接，不能处置调用方持有的连接。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenTransactionActionsSucceed_ShouldNotDisposeDbContextConnection()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        provider.GetRequiredService<ITransactionActionManager>().Register(_ => Task.CompletedTask);
        unitOfWork.Samples.Add(new SaveSample { Name = "transaction" });

        // Act
        var result = await unitOfWork.SaveChangesAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select 1";
        var scalar = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Null(unitOfWork.Database.CurrentTransaction);
        Assert.Equal(1L, scalar);
    }

    /// <summary>
    /// 测试目的：同步保存存在事务动作时必须等待并执行该动作，不能仅保存实体而跳过回调。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenTransactionActionsAreRegistered_ShouldExecuteActions()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        var transactionActionExecuted = false;
        provider.GetRequiredService<ITransactionActionManager>().Register(_ =>
        {
            transactionActionExecuted = true;
            return Task.CompletedTask;
        });
        unitOfWork.Samples.Add(new SaveSample { Name = "sync-transaction" });

        // Act
        var result = unitOfWork.SaveChanges();

        // Assert
        Assert.Equal(1, result);
        Assert.True(transactionActionExecuted);
        Assert.Null(unitOfWork.Database.CurrentTransaction);
    }

    /// <summary>
    /// 测试目的：同步事务动作写入后失败时必须回滚，不能留下动作已写入的数据。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenTransactionActionFails_ShouldRollbackActionChanges()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        provider.GetRequiredService<ITransactionActionManager>().Register(transaction =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "Insert Into save_samples (Name) Values ('sync-transaction-action')";
            command.ExecuteNonQuery();
            throw new InvalidOperationException("sync transaction action failure");
        });

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() => unitOfWork.SaveChanges());

        // Assert
        Assert.Equal("sync transaction action failure", exception.Message);
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "Select Count(*) From save_samples";
        Assert.Equal(0L, countCommand.ExecuteScalar());
    }

    /// <summary>
    /// 测试目的：手工事务自行打开连接后，必须解绑已完成事务、关闭该连接，并允许同一工作单元继续保存。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenMethodOpensConnection_ShouldCloseConnectionDetachTransactionAndAllowReuse()
    {
        // Arrange
        var connectionString = $"Data Source=file:unit_of_work_{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        await using var connection = new SqliteConnection(connectionString);
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        connection.Close();
        provider.GetRequiredService<ITransactionActionManager>().Register(_ => Task.CompletedTask);
        unitOfWork.Samples.Add(new SaveSample { Name = "transaction" });

        // Act
        var firstResult = await unitOfWork.SaveChangesAsync();

        // Assert
        Assert.Equal(1, firstResult);
        Assert.Null(unitOfWork.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, connection.State);

        // Act
        unitOfWork.Samples.Add(new SaveSample { Name = "reused" });
        var secondResult = await unitOfWork.SaveChangesAsync();

        // Assert
        Assert.Equal(1, secondResult);
        Assert.Null(unitOfWork.Database.CurrentTransaction);
        Assert.Equal(2, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 测试目的：事务动作完成后发生取消时必须使用不可取消回滚，不能保留事务动作写入。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancelledAfterTransactionAction_ShouldRollbackActionChanges()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        provider.GetRequiredService<ITransactionActionManager>().Register(async transaction =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "Insert Into save_samples (Name) Values ('transaction-action')";
            await command.ExecuteNonQueryAsync();
            cancellationTokenSource.Cancel();
        });

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.SaveChangesAsync(cancellationTokenSource.Token));
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Count(*) From save_samples";
        var count = await command.ExecuteScalarAsync();
        Assert.Equal(0L, count);
    }

    /// <summary>
    /// 测试目的：手工事务自行打开连接后发生取消时，必须回滚、解绑并关闭该连接。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenMethodOpenedConnectionAndCancelled_ShouldRollbackDetachAndCloseConnection()
    {
        // Arrange
        var connectionString = $"Data Source=file:unit_of_work_cancel_{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        await using var connection = new SqliteConnection(connectionString);
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        connection.Close();
        using var cancellationTokenSource = new CancellationTokenSource();
        provider.GetRequiredService<ITransactionActionManager>().Register(async transaction =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "Insert Into save_samples (Name) Values ('transaction-action')";
            await command.ExecuteNonQueryAsync();
            cancellationTokenSource.Cancel();
        });

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            unitOfWork.SaveChangesAsync(cancellationTokenSource.Token));
        Assert.Null(unitOfWork.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, connection.State);
        await using var countCommand = anchor.CreateCommand();
        countCommand.CommandText = "Select Count(*) From save_samples";
        Assert.Equal(0L, await countCommand.ExecuteScalarAsync());
    }

    /// <summary>
    /// 测试目的：同步保存成功后应发布一次领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenSuccessful_ShouldPublishEventsOnce()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        unitOfWork.Samples.Add(new SaveSample { Name = "sync" });

        // Act
        var result = unitOfWork.SaveChanges();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 测试目的：异步保存成功后应发布一次领域事件。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenSuccessful_ShouldPublishEventsOnce()
    {
        // Arrange
        await using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        unitOfWork.Samples.Add(new SaveSample { Name = "async" });

        // Act
        var result = await unitOfWork.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 验证同步保存后的通知失败不会被误报为数据库回滚。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenPostCommitNotificationFails_ShouldReportCommittedData()
    {
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        provider.GetRequiredService<ITransactionActionManager>().Register(_ => Task.CompletedTask);
        unitOfWork.NotificationFailure = new InvalidOperationException("notification failed");
        unitOfWork.Samples.Add(new SaveSample { Name = "committed-sync" });

        var exception = Assert.Throws<PostCommitNotificationException>(() => unitOfWork.SaveChanges());

        Assert.Same(unitOfWork.NotificationFailure, exception.InnerException);
        Assert.Contains("保存操作已完成", exception.Message);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
        Assert.Equal(1, unitOfWork.Samples.Count());
    }

    /// <summary>
    /// 验证异步保存后的通知失败保留原始异常和已提交的数据。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenPostCommitNotificationFails_ShouldReportCommittedData()
    {
        await using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        provider.GetRequiredService<ITransactionActionManager>().Register(_ => Task.CompletedTask);
        unitOfWork.NotificationFailure = new InvalidOperationException("notification failed");
        unitOfWork.Samples.Add(new SaveSample { Name = "committed-async" });

        var exception = await Assert.ThrowsAsync<PostCommitNotificationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Same(unitOfWork.NotificationFailure, exception.InnerException);
        Assert.Contains("保存操作已完成", exception.Message);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
        Assert.Equal(1, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证外部事务仍可在保存后通知失败时回滚。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenExternalTransactionExists_ShouldNotClaimFinalCommit()
    {
        await using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        await using var transaction = await unitOfWork.Database.BeginTransactionAsync();
        unitOfWork.NotificationFailure = new InvalidOperationException("notification failed");
        unitOfWork.Samples.Add(new SaveSample { Name = "external-transaction" });

        var exception = await Assert.ThrowsAsync<PostCommitNotificationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Same(unitOfWork.NotificationFailure, exception.InnerException);
        Assert.Contains("外部事务", exception.Message);
        Assert.Equal(1, await unitOfWork.Samples.CountAsync());
        await transaction.RollbackAsync();
        Assert.Equal(0, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证领域事件在数据库事务内分轮分发，并于保存成功后移除。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithDomainEvents_ShouldDispatchDerivedEventsInTransaction()
    {
        await using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        var sample = new SaveSample { Name = "domain-events" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);
        dispatcher.OnDispatch = @event =>
        {
            Assert.NotNull(unitOfWork.Database.CurrentTransaction);
            if (((SaveDomainEvent)@event).Sequence == 1)
                sample.AddDomainEvent(new SaveDomainEvent(2));
            return Task.CompletedTask;
        };

        await unitOfWork.SaveChangesAsync();

        Assert.Equal(new[] { 1, 2 }, dispatcher.Sequences);
        Assert.Empty(sample.GetDomainEvents());
        Assert.Equal(1, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证同步保存也在事务内分发并清理领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WithDomainEvents_ShouldDispatchInTransaction()
    {
        using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        var sample = new SaveSample { Name = "sync-domain-event" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);
        dispatcher.OnDispatch = _ =>
        {
            Assert.NotNull(unitOfWork.Database.CurrentTransaction);
            return Task.CompletedTask;
        };

        unitOfWork.SaveChanges();

        Assert.Equal(new[] { 1 }, dispatcher.Sequences);
        Assert.Empty(sample.GetDomainEvents());
        Assert.Equal(1, unitOfWork.Samples.Count());
    }

    /// <summary>
    /// 验证事务内分派失败阻止实体保存，并保留待处理事件。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenDomainDispatchFails_ShouldRollbackAndRetainEvents()
    {
        await using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        var sample = new SaveSample { Name = "not-committed" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);
        var original = new InvalidOperationException("dispatch failed");
        dispatcher.OnDispatch = _ => throw original;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Same(original, exception);
        Assert.Single(sample.GetDomainEvents());
        Assert.Equal(0, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证处理器持续产生事件时分派有上限且实体不会保存。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenDomainEventsRepeat_ShouldStopAtRoundLimit()
    {
        await using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        var sample = new SaveSample { Name = "recursive" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);
        dispatcher.OnDispatch = @event =>
        {
            sample.AddDomainEvent(new SaveDomainEvent(((SaveDomainEvent)@event).Sequence + 1));
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Contains("32", exception.Message);
        Assert.Equal(32, dispatcher.Sequences.Count);
        Assert.Equal(0, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证外部事务中的领域事件在回调执行前被拒绝。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithDomainEventsAndExternalTransaction_ShouldRejectBeforeDispatch()
    {
        await using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        await using var transaction = await unitOfWork.Database.BeginTransactionAsync();
        var sample = new SaveSample { Name = "external-event" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Contains("外部事务", exception.Message);
        Assert.Empty(dispatcher.Sequences);
        Assert.Single(sample.GetDomainEvents());
        Assert.Equal(0, await unitOfWork.Samples.CountAsync());
    }

    /// <summary>
    /// 验证已提交后的资源失败仍清理领域事件且不执行后续通知。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenCleanupFailsAfterCommit_ShouldClearEventsAndExposeOutcome(bool asynchronous)
    {
        await using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDomainEventDispatcher();
        using var provider = CreateServiceProvider(dispatcher);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        var sample = new SaveSample { Name = "committed-with-cleanup-error" };
        sample.AddDomainEvent(new SaveDomainEvent(1));
        unitOfWork.Samples.Add(sample);
        var original = new InvalidOperationException("cleanup failed");
        unitOfWork.AfterCommitFailure = original;

        var exception = asynchronous
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync())
            : Assert.Throws<InvalidOperationException>(() => unitOfWork.SaveChanges());

        Assert.Same(original, exception);
        Assert.Equal(TransactionOutcome.Committed, exception.GetTransactionOutcome());
        Assert.Empty(sample.GetDomainEvents());
        Assert.Equal(new[] { 1 }, dispatcher.Sequences);
        Assert.Equal(0, unitOfWork.PublishEventsCount);
        using var verification = connection.CreateCommand();
        verification.CommandText = "SELECT COUNT(*) FROM save_samples";
        Assert.Equal(1L, (long)await verification.ExecuteScalarAsync());
    }

    /// <summary>
    /// 测试目的：保存失败时不得发布领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenPersistenceFails_ShouldNotPublishEvents()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Samples.Add(new SaveSample { Name = "failure" });

        // Act and Assert
        Assert.Throws<DbUpdateException>(() => unitOfWork.SaveChanges());
        Assert.Equal(0, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 创建已打开的 SQLite 内存连接。
    /// </summary>
    /// <returns>SQLite 数据库连接。</returns>
    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateServiceProvider(IDomainEventDispatcher dispatcher = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
        if (dispatcher != null)
            services.AddSingleton(dispatcher);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建测试工作单元。
    /// </summary>
    /// <param name="connection">SQLite 数据库连接。</param>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <returns>测试工作单元。</returns>
    private static EventPublishingUnitOfWork CreateUnitOfWork(SqliteConnection connection,
        IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<EventPublishingUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new EventPublishingUnitOfWork(options, serviceProvider);
    }

    /// <summary>
    /// 可统计事件发布次数的测试工作单元。
    /// </summary>
    private sealed class EventPublishingUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="EventPublishingUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public EventPublishingUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取或设置测试实体集合。
        /// </summary>
        public DbSet<SaveSample> Samples { get; set; }

        /// <summary>
        /// 获取或设置事件发布次数。
        /// </summary>
        public int PublishEventsCount { get; private set; }

        /// <summary>
        /// 用于模拟提交后通知失败的异常。
        /// </summary>
        public Exception NotificationFailure { get; set; }

        /// <summary>
        /// 提交成功后模拟资源释放失败。
        /// </summary>
        public Exception AfterCommitFailure { get; set; }

        /// <inheritdoc />
        protected override int TransactionCommit(ITransactionActionManager transactionActionManager)
        {
            var result = base.TransactionCommit(transactionActionManager);
            ThrowAfterCommit();
            return result;
        }

        /// <inheritdoc />
        protected override async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            var result = await base.TransactionCommitAsync(transactionActionManager, cancellationToken);
            ThrowAfterCommit();
            return result;
        }

        /// <summary>
        /// 注入具有已提交诊断的清理错误。
        /// </summary>
        private void ThrowAfterCommit()
        {
            if (AfterCommitFailure == null)
                return;
            AfterCommitFailure.SetTransactionOutcome(TransactionOutcome.Committed);
            throw AfterCommitFailure;
        }

        /// <summary>
        /// 保存前拦截调用次数。
        /// </summary>
        public int SaveChangesBeforeCount { get; private set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SaveSample>(builder =>
            {
                builder.ToTable("save_samples");
                builder.HasKey(entity => entity.Id);
            });
        }

        /// <inheritdoc />
        protected override void SaveChangesBefore()
        {
            SaveChangesBeforeCount++;
        }

        /// <inheritdoc />
        protected override Task PublishEventsAsync()
        {
            PublishEventsCount++;
            if (NotificationFailure != null)
                throw NotificationFailure;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 保存测试实体。
    /// </summary>
    private sealed class SaveSample : IHasDomainEvents
    {
        /// <summary>
        /// 待分发的领域事件。
        /// </summary>
        private readonly List<DomainEvent> _events = new();

        /// <summary>
        /// 获取或设置标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 添加测试领域事件。
        /// </summary>
        public void AddDomainEvent(DomainEvent @event) => _events.Add(@event);

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _events.AsReadOnly();

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event) => _events.Remove(@event);

        /// <inheritdoc />
        public void ClearDomainEvents() => _events.Clear();
    }

    /// <summary>
    /// 测试领域事件。
    /// </summary>
    private sealed class SaveDomainEvent : DomainEvent
    {
        /// <summary>
        /// 初始化一个 <see cref="SaveDomainEvent" /> 类型的实例。
        /// </summary>
        public SaveDomainEvent(int sequence) => Sequence = sequence;

        /// <summary>
        /// 事件序号。
        /// </summary>
        public int Sequence { get; }
    }

    /// <summary>
    /// 记录领域事件分发的测试替身。
    /// </summary>
    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        /// <summary>
        /// 分发回调。
        /// </summary>
        public Func<DomainEvent, Task> OnDispatch { get; set; }

        /// <summary>
        /// 已分发事件序号。
        /// </summary>
        public List<int> Sequences { get; } = new();

        /// <inheritdoc />
        public bool Register<TDomainEvent, TDomainEventHandler>()
            where TDomainEvent : DomainEvent
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent> => throw new NotSupportedException();

        /// <inheritdoc />
        public bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public bool Register(Type eventType, Type handlerType) => throw new NotSupportedException();

        /// <inheritdoc />
        public async Task DispatchAsync(DomainEvent @event)
        {
            Sequences.Add(((SaveDomainEvent)@event).Sequence);
            if (OnDispatch != null)
                await OnDispatch(@event);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
