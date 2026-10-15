using Bing.Admin.Data;
using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Data.Common;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 CAP 与业务事务的独立清理行为。
/// </summary>
public sealed class CapUnitOfWorkExtensionsTest
{
    /// <summary>
    /// 验证 CAP 依赖缺失时不会提前打开数据库事务。
    /// </summary>
    [Fact]
    public void BeginTransaction_WhenDispatcherIsMissing_ShouldNotOpenDatabaseTransaction()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var publisher = CreatePublisher(new ServiceCollection().BuildServiceProvider(), out _);

        Assert.Throws<InvalidOperationException>(() => unitOfWork.Object.BeginTransaction(publisher.Object));

        unitOfWork.Verify(value => value.GetOrBeginTransaction(It.IsAny<bool>()), Times.Never);
    }

    /// <summary>
    /// 验证 CAP 接入失败时回滚本次新建的业务事务并恢复当前事务。
    /// </summary>
    [Fact]
    public void BeginTransaction_WhenCapBeginFails_ShouldRollbackNewDatabaseTransaction()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.GetOrBeginTransaction(false)).Returns((DbTransaction)null);
        unitOfWork.Setup(value => value.GetOrBeginTransaction(true)).Returns(new Mock<DbTransaction>().Object);
        var publisher = CreatePublisher(CreateDispatcherProvider(), out var ambient);
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var original = new InvalidOperationException("CAP begin failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupSet(value => value.DbTransaction = It.IsAny<object>()).Throws(original);

        Assert.Same(original, Assert.Throws<InvalidOperationException>(() =>
            CapUnitOfWorkExtensions.BeginTransaction(unitOfWork.Object, publisher.Object,
                _ => transaction.Object)));

        unitOfWork.Verify(value => value.Rollback(), Times.Once);
        transaction.Verify(value => value.Dispose(), Times.Once);
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证 CAP 接入失败不会回滚调用方已存在的业务事务。
    /// </summary>
    [Fact]
    public void BeginTransaction_WhenExistingDatabaseTransactionFails_ShouldKeepExistingTransaction()
    {
        var databaseTransaction = new Mock<DbTransaction>().Object;
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.GetOrBeginTransaction(It.IsAny<bool>())).Returns(databaseTransaction);
        var publisher = CreatePublisher(CreateDispatcherProvider(), out _);
        var original = new InvalidOperationException("CAP begin failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupSet(value => value.DbTransaction = It.IsAny<object>()).Throws(original);

        Assert.Same(original, Assert.Throws<InvalidOperationException>(() =>
            CapUnitOfWorkExtensions.BeginTransaction(unitOfWork.Object, publisher.Object,
                _ => transaction.Object)));

        unitOfWork.Verify(value => value.Rollback(), Times.Never);
        transaction.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 验证启动失败时回滚和释放均失败仍保留原始异常。
    /// </summary>
    [Fact]
    public void BeginTransaction_WhenCleanupFails_ShouldAttemptEveryCleanup()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.GetOrBeginTransaction(false)).Returns((DbTransaction)null);
        unitOfWork.Setup(value => value.GetOrBeginTransaction(true)).Returns(new Mock<DbTransaction>().Object);
        var publisher = CreatePublisher(CreateDispatcherProvider(), out var ambient);
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var original = new InvalidOperationException("CAP begin failed");
        var rollback = new InvalidOperationException("database rollback failed");
        var dispose = new InvalidOperationException("CAP dispose failed");
        var transaction = new Mock<ICapTransaction>();
        transaction.SetupSet(value => value.DbTransaction = It.IsAny<object>()).Throws(original);
        transaction.Setup(value => value.Dispose()).Throws(dispose);
        unitOfWork.Setup(value => value.Rollback()).Throws(rollback);

        var failure = Assert.Throws<AggregateException>(() =>
            CapUnitOfWorkExtensions.BeginTransaction(unitOfWork.Object, publisher.Object,
                _ => transaction.Object));

        Assert.Equal(new Exception[] { original, rollback, dispose }, failure.InnerExceptions);
        unitOfWork.Verify(value => value.Rollback(), Times.Once);
        transaction.Verify(value => value.Dispose(), Times.Once);
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证数据库事务启动失败时不会发布新的 CAP 当前事务。
    /// </summary>
    [Fact]
    public void BeginTransaction_WhenDatabaseBeginFails_ShouldPreserveAmbientTransaction()
    {
        var original = new InvalidOperationException("database begin failed");
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.GetOrBeginTransaction(It.IsAny<bool>())).Throws(original);
        var ambient = new System.Threading.AsyncLocal<ICapTransaction>();
        var previous = new Mock<ICapTransaction>().Object;
        ambient.Value = previous;
        var publisher = new Mock<ICapPublisher>();
        publisher.SetupGet(value => value.Transaction).Returns(ambient);
        publisher.SetupGet(value => value.ServiceProvider).Returns(CreateDispatcherProvider());

        Assert.Same(original, Assert.Throws<InvalidOperationException>(() =>
            unitOfWork.Object.BeginTransaction(publisher.Object)));
        Assert.Same(previous, ambient.Value);
    }

    /// <summary>
    /// 验证 CAP 回滚失败后仍尝试业务回滚并保留全部异常。
    /// </summary>
    [Fact]
    public void RollbackBoth_ShouldContinueAfterCapFailure()
    {
        var original = new OperationCanceledException("business canceled");
        var capFailure = new InvalidOperationException("cap rollback failed");
        var databaseFailure = new InvalidOperationException("database rollback failed");
        var order = new List<string>();

        var result = CapUnitOfWorkExtensions.RollbackBoth(original,
            () =>
            {
                order.Add("CAP");
                throw capFailure;
            },
            () =>
            {
                order.Add("Database");
                throw databaseFailure;
            });

        Assert.Equal(new[] { "CAP", "Database" }, order);
        Assert.Equal(new Exception[] { original, capFailure, databaseFailure }, result.InnerExceptions);
    }

    /// <summary>
    /// 验证清理成功时调用方可重新抛出原始异常。
    /// </summary>
    [Fact]
    public void RollbackBoth_ShouldReturnNoAggregateWhenCleanupSucceeds()
    {
        var count = 0;
        var result = CapUnitOfWorkExtensions.RollbackBoth(new InvalidOperationException("original"),
            () => count++, () => count++);

        Assert.Null(result);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// 验证 CAP 内部 Flush 契约缺失时立即报告兼容错误。
    /// </summary>
    [Fact]
    public void Flush_ShouldRejectTransactionWithoutFlushMethod()
    {
        var transaction = new Mock<ICapTransaction>().Object;

        Assert.Throws<MissingMethodException>(() => transaction.Flush());
    }

    /// <summary>
    /// 验证数据库提交后 Flush 失败不会被误报为已回滚。
    /// </summary>
    [Fact]
    public void Commit_WhenFlushFails_ShouldPreserveCommittedDatabaseResult()
    {
        var database = new Mock<IUnitOfWork>();
        var cap = new Mock<ICapTransaction>().Object;
        var committed = false;

        Assert.Throws<MissingMethodException>(() => database.Object.Commit(cap, () => committed = true));

        Assert.True(committed);
        database.Verify(value => value.Commit(), Times.Once);
        database.Verify(value => value.Rollback(), Times.Never);
    }

    /// <summary>
    /// 验证释放失败保留业务异常且仅尝试释放一次。
    /// </summary>
    [Fact]
    public void DisposeAndCapture_WhenDisposeFails_ShouldPreserveBothErrors()
    {
        var original = new OperationCanceledException("business canceled");
        var cleanup = new InvalidOperationException("dispose failed");
        var resource = new Mock<IDisposable>();
        resource.Setup(value => value.Dispose()).Throws(cleanup);

        var result = TransactionCleanup.DisposeAndCapture(resource.Object, original);

        Assert.Equal(new Exception[] { original, cleanup },
            Assert.IsType<AggregateException>(result).InnerExceptions);
        resource.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 验证没有业务异常时，释放失败仍作为原始错误返回。
    /// </summary>
    [Fact]
    public void DisposeAndCapture_WithoutEarlierFailure_ShouldReturnDisposeError()
    {
        var cleanup = new InvalidOperationException("dispose failed");
        var resource = new Mock<IDisposable>();
        resource.Setup(value => value.Dispose()).Throws(cleanup);

        Assert.Same(cleanup, TransactionCleanup.DisposeAndCapture(resource.Object, null));
        resource.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 验证正常释放仅执行一次且保留此前异常。
    /// </summary>
    [Fact]
    public void DisposeAndCapture_WhenDisposeSucceeds_ShouldKeepOriginalError()
    {
        var original = new InvalidOperationException("business failed");
        var resource = new Mock<IDisposable>();

        Assert.Same(original, TransactionCleanup.DisposeAndCapture(resource.Object, original));
        resource.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>
    /// 创建供事务启动测试使用的 CAP 发布器。
    /// </summary>
    private static Mock<ICapPublisher> CreatePublisher(IServiceProvider serviceProvider,
        out System.Threading.AsyncLocal<ICapTransaction> ambient)
    {
        ambient = new System.Threading.AsyncLocal<ICapTransaction>();
        var publisher = new Mock<ICapPublisher>();
        publisher.SetupGet(value => value.Transaction).Returns(ambient);
        publisher.SetupGet(value => value.ServiceProvider).Returns(serviceProvider);
        return publisher;
    }

    /// <summary>
    /// 创建具备 CAP 分发器的服务提供器。
    /// </summary>
    private static IServiceProvider CreateDispatcherProvider() => new ServiceCollection()
        .AddSingleton(new Mock<IDispatcher>().Object)
        .BuildServiceProvider();
}
