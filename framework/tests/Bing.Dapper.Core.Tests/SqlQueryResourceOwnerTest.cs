using System.Data;
using System.Reflection;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>直接验证查询所有者的可选事务策略与清理语义。</summary>
public sealed class SqlQueryResourceOwnerTest
{
    /// <summary>独立查询不得因获取或清理资源而创建事务。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandaloneQuery_ShouldNotBeginCommitOrRollbackTransaction(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext();
        var owner = (SqlQueryResourceOwner)context.Resources;

        Assert.Null(await GetTransaction(owner, asynchronous));
        await Complete(owner, asynchronous);
        await Rollback(owner, asynchronous);

        Assert.False(owner.IsPrimaryReadTransaction);
        context.VerifyNoTransactionLifecycle();
        context.Connection.Verify(value => value.Open(), Times.Never);
    }

    /// <summary>主库读事务由查询所有者按操作开启和完成，重复完成不得二次提交。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryReadQuery_ShouldCommitOwnedTransactionOnce(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        var owner = (SqlQueryResourceOwner)context.Resources;

        Assert.Same(context.Transaction.Object, await GetTransaction(owner, asynchronous));
        Assert.Same(context.Transaction.Object, await GetTransaction(owner, asynchronous));
        Assert.True(owner.IsPrimaryReadTransaction);
        await Complete(owner, asynchronous);
        await Complete(owner, asynchronous);

        Assert.False(owner.IsPrimaryReadTransaction);
        Assert.False(owner.HasTransaction);
        context.Transaction.Verify(value => value.Commit(), Times.Once);
        context.Transaction.Verify(value => value.Rollback(), Times.Never);
        context.Transaction.Verify(value => value.Dispose(), Times.Once);
        context.Connection.Verify(value => value.BeginTransaction(), asynchronous ? Times.Never : Times.Once);
        context.Connection.Verify(value => value.BeginTransaction(It.IsAny<IsolationLevel>()),
            asynchronous ? Times.Once : Times.Never);
    }

    /// <summary>失败操作回滚自有主库读事务，后续查询可以重新开始独立操作。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPrimaryReadQuery_ShouldRollbackAndAllowNextOperation(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        var owner = (SqlQueryResourceOwner)context.Resources;
        await GetTransaction(owner, asynchronous);

        await Rollback(owner, asynchronous);
        await Rollback(owner, asynchronous);
        Assert.False(owner.IsPrimaryReadTransaction);
        Assert.False(owner.HasTransaction);
        context.Transaction.Verify(value => value.Rollback(), Times.Once);

        Assert.Same(context.Transaction.Object, await GetTransaction(owner, asynchronous));
        await Complete(owner, asynchronous);
        context.Transaction.Verify(value => value.Commit(), Times.Once);
        context.Transaction.Verify(value => value.Dispose(), Times.Exactly(2));
    }

    /// <summary>提交失败仍尝试回滚并清除查询状态，后续失败处理不得重复回滚。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitFailure_ShouldResetQueryStateAndPreserveOriginalException(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        var expected = new InvalidOperationException("commit failed");
        context.Transaction.Setup(value => value.Commit()).Throws(expected);
        var owner = (SqlQueryResourceOwner)context.Resources;
        await GetTransaction(owner, asynchronous);

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => Complete(owner, asynchronous)));
        await Rollback(owner, asynchronous);

        Assert.False(owner.IsPrimaryReadTransaction);
        Assert.False(owner.HasTransaction);
        context.Transaction.Verify(value => value.Rollback(), Times.Once);
        context.Transaction.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>回滚失败不得遗留主库读事务标记或覆盖原始回滚异常。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RollbackFailure_ShouldResetQueryStateAndPreserveOriginalException(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        var expected = new InvalidOperationException("rollback failed");
        context.Transaction.Setup(value => value.Rollback()).Throws(expected);
        var owner = (SqlQueryResourceOwner)context.Resources;
        await GetTransaction(owner, asynchronous);

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => Rollback(owner, asynchronous)));

        Assert.False(owner.IsPrimaryReadTransaction);
        Assert.False(owner.HasTransaction);
        context.Transaction.Verify(value => value.Dispose(), Times.Once);
    }

    /// <summary>显式共享事务优先于主库读策略，查询清理和释放不得完成外部事务。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedTransaction_ShouldBeBorrowedWithoutOwnedCleanup(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        context.Resources.BindExternalTransaction(context.Transaction.Object, "shared");
        var owner = (SqlQueryResourceOwner)context.Resources;

        Assert.Same(context.Transaction.Object, await GetTransaction(owner, asynchronous));
        await Complete(owner, asynchronous);
        await Rollback(owner, asynchronous);
        context.Resources.Dispose();

        Assert.False(owner.IsPrimaryReadTransaction);
        context.VerifyNoTransactionLifecycle();
        context.Connection.Verify(value => value.Dispose(), Times.Never);
    }

    /// <summary>异步预取消不能开启事务或遗留查询策略状态。</summary>
    [Fact]
    public async Task PreCanceledQuery_ShouldNotStartPrimaryReadTransaction()
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true);
        var owner = (SqlQueryResourceOwner)context.Resources;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => owner.GetQueryTransactionAsync(cancellation.Token));

        Assert.False(owner.IsPrimaryReadTransaction);
        context.VerifyNoTransactionLifecycle();
    }

    /// <summary>查询策略及状态必须物理归属于查询所有者，不能继续留在共享资源核心。</summary>
    [Fact]
    public void QueryOwner_ShouldOwnPrimaryReadStateAndPolicy()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.Equal(typeof(bool), typeof(SqlQueryResourceOwner).GetField("_primaryReadTransactionStarted", flags)?.FieldType);
        Assert.Null(typeof(SqlTransactionResourceState).GetField("PrimaryReadTransactionStarted", flags));
        Assert.Null(typeof(SqlExecutionResources).GetMethod("GetQueryTransaction", flags));
        Assert.Null(typeof(SqlExecutionResources).GetMethod("CompleteQueryTransaction", flags));
        Assert.Null(typeof(SqlExecutionResources).GetMethod("RollbackQueryTransaction", flags));
        Assert.Null(typeof(SqlExecutionBase).GetMethod("GetQueryTransaction", flags));
        Assert.Null(typeof(SqlExecutorBase).GetMethod("GetQueryTransaction", flags));
    }

    private static Task<IDbTransaction> GetTransaction(SqlQueryResourceOwner owner, bool asynchronous) =>
        asynchronous ? owner.GetQueryTransactionAsync(default) : Task.FromResult(owner.GetQueryTransaction());

    private static Task Complete(SqlQueryResourceOwner owner, bool asynchronous)
    {
        if (asynchronous)
            return owner.CompleteQueryTransactionAsync(default);
        owner.CompleteQueryTransaction();
        return Task.CompletedTask;
    }

    private static Task Rollback(SqlQueryResourceOwner owner, bool asynchronous)
    {
        if (asynchronous)
            return owner.RollbackQueryTransactionAsync();
        owner.RollbackQueryTransaction();
        return Task.CompletedTask;
    }
}
