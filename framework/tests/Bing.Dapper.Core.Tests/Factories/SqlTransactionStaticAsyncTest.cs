using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Moq;
using Xunit;

namespace Bing.Dapper.Tests.Factories;

/// <summary>验证 Provider 静态异步事务回调的调用、回退与取消语义。</summary>
public class SqlTransactionStaticAsyncTest
{
    [Fact]
    public async Task Begin_WithStaticCallback_PassesIsolationAndDefaultWithoutReflection()
    {
        var connection = new Mock<IDbConnection>(MockBehavior.Strict).Object;
        var transaction = new Mock<IDbTransaction>(MockBehavior.Strict).Object;
        IsolationLevel? seen = null;
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        var capabilities = new SqlProviderTransactionCapabilities
        {
            SupportsNativeAsyncBegin = true,
            BeginAsync = (actual, isolation, token) =>
            {
                Assert.Same(connection, actual);
                Assert.Equal(cancellation.Token, token);
                seen = isolation;
                calls++;
                return Task.FromResult(transaction);
            }
        };

        var explicitResult = await SqlTransactionAsyncAdapter.BeginWithModeAsync(connection,
            IsolationLevel.Serializable, cancellation.Token, capabilities, "test");
        Assert.Equal(IsolationLevel.Serializable, seen);
        Assert.Same(transaction, explicitResult.Result);
        Assert.Equal(SqlTransactionExecutionMode.NativeAsync, explicitResult.Mode);
        var defaultResult = await SqlTransactionAsyncAdapter.BeginDefaultWithModeAsync(connection,
            cancellation.Token, capabilities, "test");
        Assert.Null(seen);
        Assert.Same(transaction, defaultResult.Result);
        Assert.Equal(SqlTransactionExecutionMode.NativeAsync, defaultResult.Mode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CommitAndRollback_WithStaticCallbacks_PreserveTokensAndNativeMode()
    {
        var transaction = new Mock<IDbTransaction>(MockBehavior.Strict).Object;
        using var cancellation = new CancellationTokenSource();
        var commits = 0;
        var rollbacks = 0;
        var capabilities = new SqlProviderTransactionCapabilities
        {
            SupportsNativeAsyncCommit = true,
            SupportsNativeAsyncRollback = true,
            CommitAsync = (actual, token) => { Assert.Same(transaction, actual); Assert.Equal(cancellation.Token, token); commits++; return Task.CompletedTask; },
            RollbackAsync = (actual, token) => { Assert.Same(transaction, actual); Assert.Equal(cancellation.Token, token); rollbacks++; return Task.CompletedTask; }
        };

        var commit = await SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction, cancellation.Token, capabilities, "test");
        var rollback = await SqlTransactionAsyncAdapter.RollbackWithModeAsync(transaction, cancellation.Token, capabilities, "test");
        Assert.Equal(SqlTransactionExecutionMode.NativeAsync, commit.Mode);
        Assert.Equal(SqlTransactionExecutionMode.NativeAsync, rollback.Mode);
        Assert.Equal(1, commits);
        Assert.Equal(1, rollbacks);
    }

    [Fact]
    public async Task StaticCallbacks_RespectDisabledCapabilityAndCancellation()
    {
        var transaction = new Mock<IDbTransaction>(MockBehavior.Strict);
        transaction.Setup(item => item.Commit());
        var calls = 0;
        var capabilities = new SqlProviderTransactionCapabilities
        {
            SupportsNativeAsyncCommit = false,
            CommitAsync = (_, _) => { calls++; return Task.CompletedTask; }
        };
        var result = await SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction.Object, CancellationToken.None, capabilities, "test");
        Assert.Equal(SqlTransactionExecutionMode.SynchronousFallback, result.Mode);
        transaction.Verify(item => item.Commit(), Times.Once);
        Assert.Equal(0, calls);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction.Object, cancellation.Token, capabilities, "test"));
        transaction.Verify(item => item.Commit(), Times.Once);
    }

    [Fact]
    public async Task Commit_WithStaticCallback_PropagatesOriginalFailure()
    {
        var transaction = new Mock<IDbTransaction>(MockBehavior.Strict).Object;
        var failure = new InvalidOperationException("commit failed");
        var capabilities = new SqlProviderTransactionCapabilities
        {
            SupportsNativeAsyncCommit = true,
            CommitAsync = (_, _) => Task.FromException(failure)
        };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqlTransactionAsyncAdapter.CommitWithModeAsync(transaction, CancellationToken.None, capabilities, "test"));
        Assert.Same(failure, actual);
    }
}
