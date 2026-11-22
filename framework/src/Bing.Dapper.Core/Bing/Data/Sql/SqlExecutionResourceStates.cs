using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 单个查询或写入所有者的连接状态。
/// </summary>
internal struct SqlConnectionResourceState
{
    /// <summary>当前数据库连接。</summary>
    internal IDbConnection Connection;

    /// <summary>数据库连接所有权。</summary>
    internal SqlResourceOwnership ConnectionOwnership;

    /// <summary>连接来源。</summary>
    internal SqlConnectionSource ConnectionSource;
}

/// <summary>
/// 单个查询或写入所有者的事务状态。
/// </summary>
internal struct SqlTransactionResourceState
{
    /// <summary>当前数据库事务。</summary>
    internal IDbTransaction Transaction;

    /// <summary>当前事务诊断标识。</summary>
    internal string TransactionId;

    /// <summary>当前事务最近一次异步操作的执行模式。</summary>
    internal string TransactionExecutionMode;

    /// <summary>事务作用域租约。</summary>
    internal ISqlTransactionScopeLease TransactionScopeLease;

    /// <summary>事务作用域子对象是否已经释放。</summary>
    internal bool IsTransactionScopeChildDisposed;

    /// <summary>事务所有权。</summary>
    internal SqlResourceOwnership TransactionOwnership;

    /// <summary>外部事务延迟解析器。</summary>
    internal Func<IDbTransaction> ExternalTransactionResolver;
}
