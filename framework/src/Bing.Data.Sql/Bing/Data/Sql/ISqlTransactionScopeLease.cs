namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域执行租约。
/// </summary>
public interface ISqlTransactionScopeLease
{
    /// <summary>
    /// 事务作用域标识。
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 确保作用域仍处于活动状态。
    /// </summary>
    void EnsureActive();
}