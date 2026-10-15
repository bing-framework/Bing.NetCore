using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.FreeSQL;
using DotNetCore.CAP;
using FreeSql;
using FreeSql.DataAnnotations;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 Admin FreeSQL 工作单元的组合清理路径。
/// </summary>
public sealed class AdminUnitOfWorkCleanupTest
{
    /// <summary>
    /// 验证 CAP 接入失败时完整工作单元恢复当前事务且不执行事务动作。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCapBeginFails_ShouldRestoreAmbientTransaction()
    {
        using var orm = CreateOrm();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var ambient = scope.ServiceProvider.GetRequiredService<ICapPublisher>().Transaction;
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var original = new InvalidOperationException("CAP begin failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupSet(value => value.DbTransaction = It.IsAny<object>()).Throws(original);
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        var actionCount = 0;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>().Register(_ =>
        {
            actionCount++;
            return Task.CompletedTask;
        });

        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.SaveChangesAsync()));

        Assert.Equal(0, actionCount);
        transaction.Verify(value => value.Dispose(), Times.Once);
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证业务、CAP 回滚和释放失败均可定位且当前事务被恢复。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenBusinessAndCleanupFail_ShouldKeepEveryFailure()
    {
        using var orm = CreateOrm();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var ambient = scope.ServiceProvider.GetRequiredService<ICapPublisher>().Transaction;
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var original = new InvalidOperationException("business failed");
        var rollback = new InvalidOperationException("CAP rollback failed");
        var dispose = new InvalidOperationException("CAP dispose failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        transaction.Setup(value => value.Rollback()).Throws(rollback);
        transaction.Setup(value => value.Dispose()).Throws(dispose);
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.FromException(original));

        var failure = await Assert.ThrowsAsync<AggregateException>(() => unitOfWork.SaveChangesAsync());

        Assert.Equal(new Exception[] { original, rollback },
            Assert.IsType<AggregateException>(failure.InnerExceptions[0]).InnerExceptions);
        Assert.Same(dispose, failure.InnerExceptions[1]);
        Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
        transaction.Verify(value => value.Rollback(), Times.Once);
        transaction.Verify(value => value.Dispose(), Times.Once);
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证业务错误与 CAP、数据库回滚和释放错误在一次调用中均被保留。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenAllCleanupStepsFail_ShouldPreserveEveryError()
    {
        using var orm = CreateOrm();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var originalUnitOfWork = unitOfWork.UnitOfWork;
        var databaseTransaction = new Mock<DbTransaction>().Object;
        var database = new Mock<FreeSql.IUnitOfWork>();
        database.Setup(value => value.GetOrBeginTransaction(false)).Returns((DbTransaction)null);
        database.Setup(value => value.GetOrBeginTransaction(true)).Returns(databaseTransaction);
        var databaseRollback = new InvalidOperationException("database rollback failed");
        database.Setup(value => value.Rollback()).Throws(databaseRollback);
        unitOfWork.UnitOfWork = database.Object;
        var ambient = scope.ServiceProvider.GetRequiredService<ICapPublisher>().Transaction;
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var original = new InvalidOperationException("business failed");
        var capRollback = new InvalidOperationException("CAP rollback failed");
        var dispose = new InvalidOperationException("CAP dispose failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        transaction.Setup(value => value.Rollback()).Throws(capRollback);
        transaction.Setup(value => value.Dispose()).Throws(dispose);
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.FromException(original));

        try
        {
            var failure = await Assert.ThrowsAsync<AggregateException>(() => unitOfWork.SaveChangesAsync());

            Assert.Equal(new Exception[] { original, capRollback, databaseRollback },
                Assert.IsType<AggregateException>(failure.InnerExceptions[0]).InnerExceptions);
            Assert.Same(dispose, failure.InnerExceptions[1]);
            Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
            database.Verify(value => value.Rollback(), Times.Once);
            transaction.Verify(value => value.Rollback(), Times.Once);
            transaction.Verify(value => value.Dispose(), Times.Once);
            Assert.Same(previous, ambient.Value);
        }
        finally
        {
            unitOfWork.UnitOfWork = originalUnitOfWork;
        }
    }

    /// <summary>
    /// 验证执行中取消仍尝试回滚和释放，并保留取消根因。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCanceledAndDisposeFails_ShouldKeepCancellation()
    {
        using var orm = CreateOrm();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var ambient = scope.ServiceProvider.GetRequiredService<ICapPublisher>().Transaction;
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        var dispose = new InvalidOperationException("CAP dispose failed");
        transaction.Setup(value => value.Dispose()).Throws(dispose);
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        using var cancellation = new CancellationTokenSource();
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>().Register(_ =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        });

        var failure = await Assert.ThrowsAsync<AggregateException>(() =>
            unitOfWork.SaveChangesAsync(cancellation.Token));

        Assert.IsType<OperationCanceledException>(failure.Flatten().InnerExceptions[0]);
        Assert.Same(dispose, failure.Flatten().InnerExceptions[1]);
        transaction.Verify(value => value.Rollback(), Times.Once);
        transaction.Verify(value => value.Dispose(), Times.Once);
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证业务失败回滚 SQLite 写入并标记事务结果。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenBusinessFails_ShouldRollBackRowAndMarkOutcome()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<CommitRecord>();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        var original = new InvalidOperationException("business failed");
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.FromException(original));
        unitOfWork.Add(new CommitRecord { Value = "rolled back" });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Same(original, failure);
        Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
        Assert.Equal(0, orm.Select<CommitRecord>().Count());
        transaction.Verify(value => value.Rollback(), Times.Once);
    }

    /// <summary>
    /// 验证异步提交后的 CAP 刷新失败保留已提交的业务记录。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenFlushFailsAfterCommit_ShouldMarkCommittedAndKeepRow()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<CommitRecord>();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var originalUnitOfWork = unitOfWork.UnitOfWork;
        var commitCount = 0;
        unitOfWork.UnitOfWork = CreateCommitSpy(originalUnitOfWork, () => commitCount++).Object;
        var ambient = scope.ServiceProvider.GetRequiredService<ICapPublisher>().Transaction;
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.CompletedTask);
        unitOfWork.Add(new CommitRecord { Value = "async committed" });

        try
        {
            var failure = await Assert.ThrowsAsync<MissingMethodException>(() => unitOfWork.SaveChangesAsync());

            Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
            Assert.Equal("async committed", orm.Select<CommitRecord>().First().Value);
            Assert.Equal(1, commitCount);
            transaction.Verify(value => value.Rollback(), Times.Never);
            transaction.Verify(value => value.Dispose(), Times.Once);
            Assert.Same(previous, ambient.Value);
        }
        finally
        {
            unitOfWork.UnitOfWork = originalUnitOfWork;
        }
    }

    /// <summary>
    /// 验证同步提交后的 CAP 刷新失败保留已提交的业务记录。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenFlushFailsAfterCommit_ShouldMarkCommittedAndKeepRow()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<CommitRecord>();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.CompletedTask);
        unitOfWork.Add(new CommitRecord { Value = "sync committed" });

        var failure = Assert.Throws<MissingMethodException>(() => unitOfWork.SaveChanges());

        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal("sync committed", orm.Select<CommitRecord>().First().Value);
        transaction.Verify(value => value.Rollback(), Times.Never);
        transaction.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 验证 CAP 刷新及释放同时失败仍保留数据库提交结果。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenFlushAndDisposeFail_ShouldKeepErrorsAndCommittedOutcome()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<CommitRecord>();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var dispose = new InvalidOperationException("CAP dispose failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        transaction.Setup(value => value.Dispose()).Throws(dispose);
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.CompletedTask);
        unitOfWork.Add(new CommitRecord { Value = "committed with cleanup failure" });

        var failure = await Assert.ThrowsAsync<AggregateException>(() => unitOfWork.SaveChangesAsync());

        Assert.IsType<MissingMethodException>(failure.InnerExceptions[0]);
        Assert.Same(dispose, failure.InnerExceptions[1]);
        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal("committed with cleanup failure", orm.Select<CommitRecord>().First().Value);
        transaction.Verify(value => value.Rollback(), Times.Never);
        transaction.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 验证提交回调未完成时的事务结果为未知。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenDatabaseCommitThrows_ShouldMarkOutcomeUnknown()
    {
        using var orm = CreateOrm();
        using var provider = CreateServices(orm).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateUnitOfWork(scope);
        var originalUnitOfWork = unitOfWork.UnitOfWork;
        var database = new Mock<FreeSql.IUnitOfWork>();
        database.Setup(value => value.GetOrBeginTransaction(false)).Returns((DbTransaction)null);
        database.Setup(value => value.GetOrBeginTransaction(true)).Returns(new Mock<DbTransaction>().Object);
        var original = new InvalidOperationException("database commit uncertain");
        database.Setup(value => value.Commit()).Throws(original);
        unitOfWork.UnitOfWork = database.Object;
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>()
            .Register(_ => Task.CompletedTask);

        try
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());

            Assert.Same(original, failure);
            Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
            database.Verify(value => value.Commit(), Times.Once);
        }
        finally
        {
            unitOfWork.UnitOfWork = originalUnitOfWork;
        }
    }

    /// <summary>
    /// 验证提交后的 CAP 故障不会重新分发已处理的领域事件。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenFlushFailsAfterDomainEvent_ShouldClearDispatchedEvent()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<CommitRecord>();
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var dispatchCount = 0;
        dispatcher.Setup(value => value.DispatchAsync(It.IsAny<DomainEvent>())).Returns(() =>
        {
            dispatchCount++;
            return Task.CompletedTask;
        });
        var services = CreateServices(orm);
        services.AddSingleton(dispatcher.Object);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = CreateRecordingUnitOfWork(scope);
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupAllProperties();
        unitOfWork.CapTransactionFactory = _ => transaction.Object;
        var source = new TestDomainEventSource();
        unitOfWork.RegisterDomainEventSource(source);
        scope.ServiceProvider.GetRequiredService<ITransactionActionManager>().Register(_ =>
        {
            unitOfWork.Add(new CommitRecord { Value = "domain event committed" });
            return Task.CompletedTask;
        });

        var failure = await Assert.ThrowsAsync<MissingMethodException>(() => unitOfWork.SaveChangesAsync());

        Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
        Assert.Equal(1, dispatchCount);
        Assert.Empty(source.GetDomainEvents());
        Assert.Equal("domain event committed", orm.Select<CommitRecord>().First().Value);
        Assert.Equal(0, unitOfWork.AfterSaveCount);
        dispatcher.Verify(value => value.DispatchAsync(It.IsAny<DomainEvent>()), Times.Once);
    }

    /// <summary>
    /// 创建 SQLite 测试数据库。
    /// </summary>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
        .Build();

    /// <summary>
    /// 注册工作单元及作用域事务依赖。
    /// </summary>
    private static ServiceCollection CreateServices(IFreeSql orm)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new FreeSqlWrapper { Orm = orm });
        services.AddSingleton(new Mock<DotNetCore.CAP.Transport.IDispatcher>().Object);
        services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
        services.AddScoped<ICapPublisher>(serviceProvider =>
        {
            var publisher = new Mock<ICapPublisher>();
            publisher.SetupGet(value => value.ServiceProvider).Returns(serviceProvider);
            publisher.SetupGet(value => value.Transaction)
                .Returns(new System.Threading.AsyncLocal<ICapTransaction>());
            return publisher.Object;
        });
        services.AddScoped<AdminUnitOfWork>();
        services.AddScoped<RecordingAdminUnitOfWork>();
        return services;
    }

    /// <summary>
    /// 从当前作用域创建带延迟解析器的工作单元。
    /// </summary>
    private static AdminUnitOfWork CreateUnitOfWork(IServiceScope scope)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
        unitOfWork.LazyServiceProvider = scope.ServiceProvider.GetRequiredService<ILazyServiceProvider>();
        return unitOfWork;
    }

    /// <summary>
    /// 记录真实 SQLite 工作单元的提交次数。
    /// </summary>
    private static Mock<FreeSql.IUnitOfWork> CreateCommitSpy(FreeSql.IUnitOfWork inner, Action onCommit)
    {
        var spy = new Mock<FreeSql.IUnitOfWork>();
        spy.SetupGet(value => value.Orm).Returns(() => inner.Orm);
        spy.SetupGet(value => value.IsolationLevel).Returns(() => inner.IsolationLevel);
        spy.SetupSet(value => value.IsolationLevel = It.IsAny<System.Data.IsolationLevel?>())
            .Callback<System.Data.IsolationLevel?>(level => inner.IsolationLevel = level);
        spy.SetupGet(value => value.EntityChangeReport).Returns(() => inner.EntityChangeReport);
        spy.SetupGet(value => value.States).Returns(() => inner.States);
        spy.Setup(value => value.GetOrBeginTransaction(It.IsAny<bool>()))
            .Returns((bool create) => inner.GetOrBeginTransaction(create));
        spy.Setup(value => value.Commit()).Callback(() =>
        {
            onCommit();
            inner.Commit();
        });
        spy.Setup(value => value.Rollback()).Callback(inner.Rollback);
        return spy;
    }

    /// <summary>
    /// 创建可记录提交后通知的测试工作单元。
    /// </summary>
    private static RecordingAdminUnitOfWork CreateRecordingUnitOfWork(IServiceScope scope)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<RecordingAdminUnitOfWork>();
        unitOfWork.LazyServiceProvider = scope.ServiceProvider.GetRequiredService<ILazyServiceProvider>();
        return unitOfWork;
    }

    /// <summary>
    /// 记录提交后通知调用次数的工作单元。
    /// </summary>
    private sealed class RecordingAdminUnitOfWork : AdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="RecordingAdminUnitOfWork" /> 类型的实例。
        /// </summary>
        public RecordingAdminUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }

        /// <summary>
        /// 获取提交后通知次数。
        /// </summary>
        public int AfterSaveCount { get; private set; }

        /// <inheritdoc />
        protected override Task PublishEventsAsync()
        {
            AfterSaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 测试用持久化记录。
    /// </summary>
    [Table(Name = "bing_admin_uow_cleanup_record")]
    private sealed class CommitRecord
    {
        /// <summary>
        /// 获取或设置主键。
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置业务值。
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// 测试用领域事件源。
    /// </summary>
    private sealed class TestDomainEventSource : IHasDomainEvents
    {
        /// <summary>
        /// 待分发事件。
        /// </summary>
        private readonly List<DomainEvent> _events = new() { new TestDomainEvent() };

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _events;

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event) => _events.Remove(@event);

        /// <inheritdoc />
        public void ClearDomainEvents() => _events.Clear();
    }

    /// <summary>
    /// 测试用领域事件。
    /// </summary>
    private sealed class TestDomainEvent : DomainEvent
    {
    }
}
