using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>直接验证写入资源不依赖查询专用事务策略。</summary>
public sealed class SqlWriteResourceOwnerTest
{
    /// <summary>写入不应因为配置了主库读取策略而创建查询短事务。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteTransaction_ShouldNotUsePrimaryReadStrategy(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true, query: false);
        var owner = (SqlWriteResourceOwner)context.Resources;

        var transaction = asynchronous
            ? await owner.GetWriteTransactionAsync(default)
            : owner.GetWriteTransaction();

        Assert.Null(transaction);
        context.VerifyNoTransactionLifecycle();
    }

    /// <summary>写入必须沿用外部事务身份，不接管事务的完成或释放。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedTransaction_ShouldRemainOwnedByCaller(bool asynchronous)
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true, query: false);
        context.Resources.BindExternalTransaction(context.Transaction.Object, "shared");
        var owner = (SqlWriteResourceOwner)context.Resources;

        var transaction = asynchronous
            ? await owner.GetWriteTransactionAsync(default)
            : owner.GetWriteTransaction();
        Assert.Same(context.Transaction.Object, transaction);
        context.Resources.Dispose();

        context.VerifyNoTransactionLifecycle();
    }

    /// <summary>预取消的写入不得准备新事务。</summary>
    [Fact]
    public async Task PreCanceledWrite_ShouldNotPrepareTransaction()
    {
        using var context = new SqlResourceOwnerTestContext(primaryRead: true, query: false);
        var owner = (SqlWriteResourceOwner)context.Resources;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => owner.GetWriteTransactionAsync(cancellation.Token));

        context.VerifyNoTransactionLifecycle();
    }
}
