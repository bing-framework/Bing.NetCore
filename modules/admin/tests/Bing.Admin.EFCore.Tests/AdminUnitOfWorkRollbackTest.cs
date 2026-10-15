using Bing.Admin.Data.UnitOfWorks.MySql;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 EFCore CAP 工作单元的异常清理。
/// </summary>
public sealed class AdminUnitOfWorkRollbackTest
{
    /// <summary>
    /// 验证业务取消不阻止回滚，且清理异常不覆盖原始异常。
    /// </summary>
    [Fact]
    public async Task TryRollbackAsync_ShouldUseIndependentTokenAndPreserveErrors()
    {
        var original = new OperationCanceledException("business canceled");
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var called = false;

        var result = await AdminUnitOfWork.TryRollbackAsync(original, token =>
        {
            called = true;
            Assert.False(token.IsCancellationRequested);
            throw rollbackFailure;
        });

        Assert.True(called);
        Assert.Equal(new Exception[] { original, rollbackFailure }, result.InnerExceptions);
    }

    /// <summary>
    /// 验证成功回滚不会生成额外异常。
    /// </summary>
    [Fact]
    public async Task TryRollbackAsync_ShouldReturnNoAggregateWhenCleanupSucceeds()
    {
        var count = 0;
        var result = await AdminUnitOfWork.TryRollbackAsync(new InvalidOperationException("original"),
            _ =>
            {
                count++;
                return Task.CompletedTask;
            });

        Assert.Null(result);
        Assert.Equal(1, count);
    }
}
