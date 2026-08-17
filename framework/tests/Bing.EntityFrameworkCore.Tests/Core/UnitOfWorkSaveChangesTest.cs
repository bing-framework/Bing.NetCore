using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
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
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
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
        /// 初始化一个 <see cref="EventPublishingUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public EventPublishingUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 测试实体集合。
        /// </summary>
        public DbSet<SaveSample> Samples { get; set; }

        /// <summary>
        /// 事件发布次数。
        /// </summary>
        public int PublishEventsCount { get; private set; }

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
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 保存测试实体。
    /// </summary>
    private sealed class SaveSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}