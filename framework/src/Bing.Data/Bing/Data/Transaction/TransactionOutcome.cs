namespace Bing.Data.Transaction;

/// <summary>
/// 事务失败时能够确认的数据库结果。
/// </summary>
public enum TransactionOutcome
{
    /// <summary>
    /// 无法确认最终结果。
    /// </summary>
    Unknown,

    /// <summary>
    /// 数据库已提交。
    /// </summary>
    Committed,

    /// <summary>
    /// 数据库已回滚。
    /// </summary>
    RolledBack
}
