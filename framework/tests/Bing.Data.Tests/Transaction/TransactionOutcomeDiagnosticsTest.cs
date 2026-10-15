using Bing.Data.Transaction;
using System;
using Xunit;

namespace Bing.Data.Tests.Transaction;

/// <summary>
/// 验证不改变原始异常的事务结果诊断。
/// </summary>
public class TransactionOutcomeDiagnosticsTest
{
    /// <summary>
    /// 验证没有证据时不能推断提交或回滚。
    /// </summary>
    [Fact]
    public void GetTransactionOutcome_UnmarkedException_ShouldBeUnknown()
    {
        Assert.Equal(TransactionOutcome.Unknown, new InvalidOperationException().GetTransactionOutcome());
    }

    /// <summary>
    /// 验证事务结果保留异常实例、类型及原始堆栈。
    /// </summary>
    [Theory]
    [InlineData(TransactionOutcome.Unknown)]
    [InlineData(TransactionOutcome.Committed)]
    [InlineData(TransactionOutcome.RolledBack)]
    public void SetTransactionOutcome_ShouldPreserveOriginalException(TransactionOutcome outcome)
    {
        var exception = new OperationCanceledException("original");
        exception.SetTransactionOutcome(outcome);
        Assert.Equal(outcome, exception.GetTransactionOutcome());
        Assert.Equal("original", exception.Message);
        Assert.IsType<OperationCanceledException>(exception);
    }

    /// <summary>
    /// 验证无效诊断值及空异常被拒绝。
    /// </summary>
    [Fact]
    public void Diagnostics_InvalidArguments_ShouldFail()
    {
        Assert.Throws<ArgumentNullException>(() => TransactionOutcomeDiagnostics.GetTransactionOutcome(null));
        Assert.Throws<ArgumentNullException>(() => TransactionOutcomeDiagnostics.SetTransactionOutcome(null, TransactionOutcome.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Exception().SetTransactionOutcome((TransactionOutcome)99));
    }

    /// <summary>
    /// 验证保存后失败可以暴露已提交结果并保留原始错误。
    /// </summary>
    [Fact]
    public void PostCommitNotificationException_ShouldExposeExplicitOutcome()
    {
        var original = new InvalidOperationException("notification");
        var exception = new PostCommitNotificationException(original, TransactionOutcome.Committed);
        Assert.Same(original, exception.InnerException);
        Assert.Equal(TransactionOutcome.Committed, exception.GetTransactionOutcome());
        Assert.Equal(TransactionOutcome.Unknown, new PostCommitNotificationException(original).GetTransactionOutcome());
    }
}
