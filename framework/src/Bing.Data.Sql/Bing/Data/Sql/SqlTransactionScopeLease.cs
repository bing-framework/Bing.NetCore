namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域执行租约。
/// </summary>
internal sealed class SqlTransactionScopeLease
{
    /// <summary>
    /// 是否仍允许执行。
    /// </summary>
    private int _isActive = 1;

    /// <summary>
    /// 初始化一个<see cref="SqlTransactionScopeLease"/>类型的实例。
    /// </summary>
    /// <param name="transactionId">事务作用域标识。</param>
    public SqlTransactionScopeLease(string transactionId) => TransactionId = transactionId;

    /// <summary>
    /// 事务作用域标识。
    /// </summary>
    public string TransactionId { get; }

    /// <summary>
    /// 使事务作用域租约失效。
    /// </summary>
    public void Invalidate() => Interlocked.Exchange(ref _isActive, 0);

    /// <summary>
    /// 确保事务作用域仍允许执行。
    /// </summary>
    public void EnsureActive()
    {
        if (Volatile.Read(ref _isActive) == 0)
            throw new InvalidOperationException("SQL 事务作用域已结束，不能继续使用其创建的 Query 或 Executor。");
    }
}