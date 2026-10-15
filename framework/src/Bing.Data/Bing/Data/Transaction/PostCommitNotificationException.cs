namespace Bing.Data.Transaction;

/// <summary>
/// 表示保存操作完成后，通知处理失败。
/// </summary>
public sealed class PostCommitNotificationException : Exception
{
    /// <summary>
    /// 初始化一个 <see cref="PostCommitNotificationException" /> 类型的实例。
    /// </summary>
    /// <param name="innerException">通知处理失败的原始异常。</param>
    public PostCommitNotificationException(Exception innerException)
        : base("数据库保存操作已完成，但后续通知处理失败；若存在外部事务，其最终提交状态由外部事务决定。", innerException)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="PostCommitNotificationException" /> 类型的实例。
    /// </summary>
    /// <param name="innerException">通知处理失败的原始异常。</param>
    /// <param name="outcome">能够确认的数据库结果。</param>
    public PostCommitNotificationException(Exception innerException, TransactionOutcome outcome)
        : this(innerException)
    {
        this.SetTransactionOutcome(outcome);
    }
}
