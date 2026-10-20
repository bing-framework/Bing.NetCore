using System.Data;
using System.Data.Common;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Transaction;
using Bing.Data.Stores;
using Bing.Domain.Entities.Events;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.FreeSQL;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Core;

/// <summary>
/// FreeSQL 工作单元取消行为单元测试。
/// </summary>
public sealed class UnitOfWorkCancellationTest
{
    /// <summary>
    /// 验证 FreeSQL 存储器会自动登记新增、修改和删除实体的领域事件源。
    /// </summary>
    [Fact]
    public async Task StoreChanges_WithDomainEventEntity_ShouldDispatchWithoutExplicitRegistration()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<StoreDomainEventSample>();
            var dispatcher = new RecordingDomainEventDispatcher();
            var services = new ServiceCollection();
            services.AddMySqlProvider();
            services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql,
                "Server=scope;Database=test;");
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
            using var store = new StoreDomainEventStore(unitOfWork,
                scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
                scope.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>(),
                scope.ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
                scope.ServiceProvider.GetRequiredService<ITypeConverterResolver>());

            var entity = new StoreDomainEventSample { Id = 1, Name = "created" };
            entity.Add(new TestDomainEvent());
            store.Add(entity);
            await unitOfWork.SaveChangesAsync();

            entity.Name = "updated";
            entity.Add(new TestDomainEvent());
            await store.UpdateAsync(entity);
            unitOfWork.SaveChanges();

            entity.Add(new TestDomainEvent());
            await store.RemoveAsync(entity);
            await unitOfWork.SaveChangesAsync();

            Assert.Equal(3, dispatcher.DispatchCount);
            Assert.Empty(entity.GetDomainEvents());
            Assert.Equal(0, orm.Select<StoreDomainEventSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证 FreeSQL 存储器会自动登记批量新增、修改和删除实体的领域事件源。
    /// </summary>
    [Fact]
    public async Task StoreChanges_WithDomainEventEntityCollection_ShouldDispatchWithoutExplicitRegistration()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<StoreDomainEventSample>();
            var dispatcher = new RecordingDomainEventDispatcher();
            var services = new ServiceCollection();
            services.AddMySqlProvider();
            services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql,
                "Server=scope;Database=test;");
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
            using var store = new StoreDomainEventStore(unitOfWork,
                scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
                scope.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>(),
                scope.ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
                scope.ServiceProvider.GetRequiredService<ITypeConverterResolver>());

            var entities = Enumerable.Range(1, 3)
                .Select(id => new StoreDomainEventSample { Id = id, Name = $"created-{id}" })
                .ToList();
            foreach (var entity in entities)
                entity.Add(new TestDomainEvent());
            store.Add(entities);
            await unitOfWork.SaveChangesAsync();

            foreach (var entity in entities)
            {
                entity.Name = $"updated-{entity.Id}";
                entity.Add(new TestDomainEvent());
            }
            await store.UpdateAsync(entities);
            unitOfWork.SaveChanges();

            foreach (var entity in entities)
                entity.Add(new TestDomainEvent());
            await store.RemoveAsync(entities);
            await unitOfWork.SaveChangesAsync();

            Assert.Equal(9, dispatcher.DispatchCount);
            Assert.All(entities, entity => Assert.Empty(entity.GetDomainEvents()));
            Assert.Equal(0, orm.Select<StoreDomainEventSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证同步保存也分发并清理显式登记的领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WithDomainEventSource_ShouldDispatchOnce()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var dispatcher = new RecordingDomainEventDispatcher();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var source = new TestDomainEventSource();
            source.Add(new TestDomainEvent());
            unitOfWork.RegisterDomainEventSource(source);

            unitOfWork.SaveChanges();

            Assert.Equal(1, dispatcher.DispatchCount);
            Assert.Empty(source.GetDomainEvents());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证登记的领域事件在保存事务内只分发一次。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithDomainEventSource_ShouldDispatchOnce()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var dispatcher = new RecordingDomainEventDispatcher();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var source = new TestDomainEventSource();
            source.Add(new TestDomainEvent());
            unitOfWork.RegisterDomainEventSource(source);
            unitOfWork.RegisterDomainEventSource(source);

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.SaveChangesAsync();

            Assert.Equal(1, dispatcher.DispatchCount);
            Assert.Empty(source.GetDomainEvents());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证领域事件分发失败时事务内写入被回滚。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenDomainEventDispatchFails_ShouldRollback()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var original = new InvalidOperationException("domain event failure");
            var dispatcher = new RecordingDomainEventDispatcher(original);
            var actions = new TransactionActionManager();
            actions.Register(transaction => ExecuteInsertAsync(transaction, "before-event"));
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(actions);
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var source = new TestDomainEventSource();
            source.Add(new TestDomainEvent());
            unitOfWork.RegisterDomainEventSource(source);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

            Assert.Same(original, exception);
            Assert.Equal(1, dispatcher.DispatchCount);
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
            Assert.Single(source.GetDomainEvents());
            Assert.Equal(0, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证已开启外部事务时不会分发领域事件或执行保存。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithDomainEventAndExternalTransaction_ShouldRejectBeforeDispatch()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var dispatcher = new RecordingDomainEventDispatcher();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var source = new TestDomainEventSource();
            source.Add(new TestDomainEvent());
            unitOfWork.RegisterDomainEventSource(source);
            unitOfWork.BeginExternalTransaction();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

            Assert.Contains("外部 FreeSQL 事务", exception.Message);
            Assert.Equal(0, dispatcher.DispatchCount);
            Assert.Single(source.GetDomainEvents());
            Assert.Equal(0, unitOfWork.NotificationCount);
            unitOfWork.RollbackExternalTransaction();
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 验证事件清理失败时保留已提交事实和原始异常。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenDomainEventRemovalFails_ShouldReportPostCommitFailure()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var original = new InvalidOperationException("event removal failure");
            var source = new TestDomainEventSource { RemovalException = original };
            source.Add(new TestDomainEvent());
            var actions = new TransactionActionManager();
            actions.Register(transaction => ExecuteInsertAsync(transaction, "committed"));
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(actions);
            services.AddSingleton<IDomainEventDispatcher>(new RecordingDomainEventDispatcher());
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            unitOfWork.RegisterDomainEventSource(source);

            var exception = await Assert.ThrowsAsync<PostCommitNotificationException>(() => unitOfWork.SaveChangesAsync());

            Assert.Same(original, exception.InnerException);
            Assert.Equal(1, orm.Select<TransactionalSample>().Count());
            Assert.Equal(0, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：事务动作存在时，预取消令牌必须在创建事务或提交事务动作前终止异步保存。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancelledBeforeTransactionActions_ShouldNotCommitActions()
    {
        // Arrange
        var transactionActionManager = new RecordingTransactionActionManager();
        var services = new ServiceCollection();
        services.AddSingleton<ITransactionActionManager>(transactionActionManager);
        using var serviceProvider = services.BuildServiceProvider();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.SaveChangesAsync(
            cancellationTokenSource.Token));
        Assert.False(transactionActionManager.CommitRequested);
        Assert.Equal(0, unitOfWork.NotificationCount);
    }

    /// <summary>
    /// 测试目的：同步提交存在事务动作时必须等待并执行动作，不能静默跳过异步回调。
    /// </summary>
    [Fact]
    public void Commit_WhenTransactionActionsAreRegistered_ShouldExecuteActions()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act
            unitOfWork.Commit();

            // Assert
            Assert.True(transactionActionManager.CommitRequested);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：同步事务动作写入后失败时必须回滚，不能让未完成事务的数据被独立查询读取。
    /// </summary>
    [Fact]
    public void Commit_WhenTransactionActionFails_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(transaction =>
            {
                ExecuteInsertAsync(transaction, "sync-failed").GetAwaiter().GetResult();
                throw new InvalidOperationException("sync transaction action failure");
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act and Assert
            var exception = Assert.Throws<InvalidOperationException>(() => unitOfWork.Commit());

            // Assert
            Assert.Equal("sync transaction action failure", exception.Message);
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
            Assert.Equal(0, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：事务动作写入后失败时，已开启的 FreeSQL 事务必须回滚，独立连接不能读取未提交写入。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenTransactionActionFails_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(async transaction =>
            {
                await ExecuteInsertAsync(transaction, "failed");
                throw new InvalidOperationException("transaction action failure");
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

            // Assert
            Assert.Equal("transaction action failure", exception.Message);
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
            Assert.Equal(0, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：事务动作写入后触发取消时，保存操作必须回滚，不能让取消前的写入对独立查询可见。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCancelledAfterTransactionAction_ShouldRollbackActionChanges()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            using var cancellationTokenSource = new CancellationTokenSource();
            var transactionActionManager = new RecordingTransactionActionManager(async transaction =>
            {
                await ExecuteInsertAsync(transaction, "cancelled");
                cancellationTokenSource.Cancel();
            });
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            // Act and Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unitOfWork.SaveChangesAsync(
                cancellationTokenSource.Token));
            Assert.Equal(0, orm.Select<TransactionalSample>().Count());
            Assert.Equal(0, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：同步保存成功后只执行一次提交后通知。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenSuccessful_ShouldNotifyOnce()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

            unitOfWork.SaveChanges();

            Assert.Equal(1, unitOfWork.NotificationCount);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：异步提交完成后通知能读取已提交的数据，且仅执行一次。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenTransactionSucceeds_ShouldNotifyOnceAfterCommit()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(transaction =>
                ExecuteInsertAsync(transaction, "committed"));
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            unitOfWork.OnPublish = () =>
            {
                Assert.Equal(1, orm.Select<TransactionalSample>().Count());
                return Task.CompletedTask;
            };

            await unitOfWork.SaveChangesAsync();

            Assert.Equal(1, unitOfWork.NotificationCount);
            Assert.Equal(1, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：同步通知失败应报告已提交状态，并保留原始异常。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenNotificationFails_ShouldPreserveCommittedData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(transaction =>
                ExecuteInsertAsync(transaction, "sync-committed"));
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var original = new InvalidOperationException("notification failed");
            unitOfWork.OnPublish = () => throw original;

            var exception = Assert.Throws<PostCommitNotificationException>(() => unitOfWork.SaveChanges());

            Assert.Same(original, exception.InnerException);
            Assert.Equal(1, unitOfWork.NotificationCount);
            Assert.Equal(1, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：异步通知失败不回滚已经成功提交的事务。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenNotificationFails_ShouldPreserveCommittedData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<TransactionalSample>();
            var transactionActionManager = new RecordingTransactionActionManager(transaction =>
                ExecuteInsertAsync(transaction, "async-committed"));
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager>(transactionActionManager);
            using var serviceProvider = services.BuildServiceProvider();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);
            var original = new InvalidOperationException("notification failed");
            unitOfWork.OnPublish = () => Task.FromException(original);

            var exception = await Assert.ThrowsAsync<PostCommitNotificationException>(() => unitOfWork.SaveChangesAsync());

            Assert.Same(original, exception.InnerException);
            Assert.Equal(1, unitOfWork.NotificationCount);
            Assert.Equal(1, orm.Select<TransactionalSample>().Count());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 创建不打开外部连接的 MySQL FreeSQL 实例。
    /// </summary>
    /// <returns>仅用于工作单元构造的 FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionFactory(DataType.MySql, () => new MySqlConnection())
        .Build();

    /// <summary>
    /// 创建使用独立临时文件的 SQLite FreeSQL 实例。
    /// </summary>
    /// <param name="databasePath">临时数据库文件路径。</param>
    /// <returns>用于事务可见性验证的 FreeSQL 实例。</returns>
    private static IFreeSql CreateSqliteOrm(string databasePath) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
        .Build();

    /// <summary>
    /// 在当前事务中插入测试数据。
    /// </summary>
    /// <param name="transaction">当前数据库事务。</param>
    /// <param name="name">写入名称。</param>
    private static async Task ExecuteInsertAsync(IDbTransaction transaction, string name)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "Insert Into transaction_samples (Name) Values (@name)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        await ((DbCommand)command).ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 测试工作单元。
    /// </summary>
    private sealed class TestUnitOfWork : Bing.Uow.UnitOfWorkBase
    {
        /// <summary>
        /// 开启由调用方管理的测试事务。
        /// </summary>
        public void BeginExternalTransaction() => UnitOfWork.GetOrBeginTransaction();

        /// <summary>
        /// 回滚由调用方管理的测试事务。
        /// </summary>
        public void RollbackExternalTransaction() => UnitOfWork.Rollback();

        /// <summary>
        /// 提交后通知回调。
        /// </summary>
        public Func<Task> OnPublish { get; set; }

        /// <summary>
        /// 提交后通知次数。
        /// </summary>
        public int NotificationCount { get; private set; }

        /// <summary>
        /// 初始化一个 <see cref="TestUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="wrapper">FreeSQL 包装器。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public TestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }

        /// <inheritdoc />
        protected override Task PublishEventsAsync()
        {
            NotificationCount++;
            return OnPublish?.Invoke() ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录事务动作调用情况的测试替身。
    /// </summary>
    private sealed class RecordingTransactionActionManager : ITransactionActionManager
    {
        /// <summary>
        /// 事务动作。
        /// </summary>
        private readonly Func<IDbTransaction, Task> _action;

        /// <summary>
        /// 初始化一个 <see cref="RecordingTransactionActionManager" /> 类型的实例。
        /// </summary>
        /// <param name="action">事务动作。</param>
        public RecordingTransactionActionManager(Func<IDbTransaction, Task> action = null) => _action = action;

        /// <inheritdoc />
        /// <remarks>
        /// 获取表示存在一个待提交的事务动作。
        /// </remarks>
        public int Count => 1;

        /// <summary>
        /// 获取或设置是否请求提交事务动作。
        /// </summary>
        public bool CommitRequested { get; private set; }

        /// <inheritdoc />
        public void Register(Func<IDbTransaction, Task> action)
        {
        }

        /// <inheritdoc />
        public Task CommitAsync(IDbTransaction transaction)
        {
            CommitRequested = true;
            return _action?.Invoke(transaction) ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// 用于验证分发次数的领域事件处理器。
    /// </summary>
    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        /// <summary>
        /// 分发时抛出的异常。
        /// </summary>
        private readonly Exception _exception;

        /// <summary>
        /// 初始化一个 <see cref="RecordingDomainEventDispatcher" /> 类型的实例。
        /// </summary>
        /// <param name="exception">分发时抛出的异常。</param>
        public RecordingDomainEventDispatcher(Exception exception = null) => _exception = exception;

        /// <summary>
        /// 领域事件分发次数。
        /// </summary>
        public int DispatchCount { get; private set; }

        /// <inheritdoc />
        public bool Register<TDomainEvent, TDomainEventHandler>()
            where TDomainEvent : DomainEvent
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent> => true;

        /// <inheritdoc />
        public bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent => true;

        /// <inheritdoc />
        public bool Register(Type eventType, Type handlerType) => true;

        /// <inheritdoc />
        public Task DispatchAsync(DomainEvent @event)
        {
            DispatchCount++;
            return _exception == null ? Task.CompletedTask : Task.FromException(_exception);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 测试用领域事件源。
    /// </summary>
    private sealed class TestDomainEventSource : IHasDomainEvents
    {
        /// <summary>
        /// 移除事件时抛出的异常。
        /// </summary>
        public Exception RemovalException { get; set; }

        /// <summary>
        /// 尚未处理的领域事件。
        /// </summary>
        private readonly List<DomainEvent> _events = new();

        /// <summary>
        /// 添加待分发事件。
        /// </summary>
        /// <param name="domainEvent">领域事件。</param>
        public void Add(DomainEvent domainEvent) => _events.Add(domainEvent);

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _events.AsReadOnly();

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event)
        {
            if (RemovalException != null)
                throw RemovalException;
            _events.Remove(@event);
        }

        /// <inheritdoc />
        public void ClearDomainEvents() => _events.Clear();
    }

    /// <summary>
    /// 测试用领域事件。
    /// </summary>
    private sealed class TestDomainEvent : DomainEvent
    {
    }

    /// <summary>
    /// 使用 FreeSQL 存储基类验证领域事件登记的测试存储器。
    /// </summary>
    private sealed class StoreDomainEventStore : StoreBase<StoreDomainEventSample, int>
    {
        /// <summary>
        /// 初始化一个 <see cref="StoreDomainEventStore" /> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">当前工作单元。</param>
        /// <param name="sqlQueryFactory">SQL 查询对象工厂。</param>
        /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
        /// <param name="metadataOptions">SQL 元数据配置。</param>
        /// <param name="typeConverterResolver">数据类型转换器解析器。</param>
        public StoreDomainEventStore(TestUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory,
            databaseContextAccessor, metadataOptions, typeConverterResolver)
        {
        }
    }

    /// <summary>
    /// 通过存储器写入并携带领域事件的测试实体。
    /// </summary>
    [FreeSql.DataAnnotations.Table(Name = "store_domain_event_samples")]
    private sealed class StoreDomainEventSample : Bing.Domain.Entities.IKey<int>, IHasDomainEvents
    {
        /// <summary>
        /// 待分发领域事件。
        /// </summary>
        private readonly List<DomainEvent> _events = new();

        /// <summary>
        /// 实体标识。
        /// </summary>
        [FreeSql.DataAnnotations.Column(IsPrimary = true)]
        public int Id { get; set; }

        /// <summary>
        /// 实体名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 添加待分发领域事件。
        /// </summary>
        /// <param name="domainEvent">领域事件。</param>
        public void Add(DomainEvent domainEvent) => _events.Add(domainEvent);

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _events.AsReadOnly();

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event) => _events.Remove(@event);

        /// <inheritdoc />
        public void ClearDomainEvents() => _events.Clear();
    }

    /// <summary>
    /// 事务测试实体。
    /// </summary>
    [FreeSql.DataAnnotations.Table(Name = "transaction_samples")]
    private sealed class TransactionalSample
    {
        /// <summary>
        /// 获取或设置标识。
        /// </summary>
        [FreeSql.DataAnnotations.Column(IsPrimary = true)]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
    }
}
