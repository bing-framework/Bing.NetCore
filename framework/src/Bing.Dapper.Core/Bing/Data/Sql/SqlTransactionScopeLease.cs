namespace Bing.Data.Sql;

/// <summary>
/// Dapper SQL 事务作用域执行租约。
/// </summary>
internal sealed class SqlTransactionScopeLease : ISqlTransactionScopeLease
{
    private int _isActive = 1;

    /// <summary>
    /// 初始化一个 <see cref="SqlTransactionScopeLease"/> 类型的实例。
    /// </summary>
    /// <param name="transactionId">事务作用域标识。</param>
    public SqlTransactionScopeLease(string transactionId) => TransactionId = transactionId;

    /// <inheritdoc />
    public string TransactionId { get; }

    /// <summary>
    /// 使租约失效。
    /// </summary>
    public void Invalidate() => Interlocked.Exchange(ref _isActive, 0);

    /// <inheritdoc />
    public void EnsureActive()
    {
        if (Volatile.Read(ref _isActive) == 0)
            throw new InvalidOperationException("SQL 事务作用域已结束，不能继续使用其创建的 Query 或 Executor。");
    }
}