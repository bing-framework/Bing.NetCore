namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域执行租约。
/// </summary>
internal interface ISqlTransactionScopeLease
{
    /// <summary>
    /// 事务作用域标识。
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 确保作用域仍处于活动状态。
    /// </summary>
    void EnsureActive();

    /// <summary>
    /// 获取当前事务作用域的一次执行租约。
    /// </summary>
    /// <returns>操作结束时必须释放的执行租约。</returns>
    IDisposable AcquireExecutionLease();
}