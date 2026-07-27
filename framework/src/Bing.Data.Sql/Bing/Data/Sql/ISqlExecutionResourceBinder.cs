using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 执行资源绑定器。
/// </summary>
public interface ISqlQueryResourceBinder
{
    /// <summary>
    /// 绑定框架自有连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="source">连接来源。</param>
    void BindOwnedConnection(IDbConnection connection, SqlConnectionSource source);

    /// <summary>
    /// 绑定外部连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="source">连接来源。</param>
    void BindExternalConnection(IDbConnection connection, SqlConnectionSource source);

    /// <summary>
    /// 绑定外部事务。
    /// </summary>
    /// <param name="transaction">数据库事务。</param>
    /// <param name="transactionId">诊断事务标识。</param>
    void BindExternalTransaction(IDbTransaction transaction, string transactionId = null);

    /// <summary>
    /// 绑定外部事务延迟解析器。
    /// </summary>
    /// <param name="resolver">外部事务解析器。</param>
    void BindExternalTransactionResolver(Func<IDbTransaction> resolver);

}

/// <summary>
/// SQL 事务作用域资源绑定器。
/// </summary>
public interface ISqlTransactionScopeResourceBinder : ISqlQueryResourceBinder
{
    /// <summary>
    /// 绑定事务作用域上下文。
    /// </summary>
    /// <param name="context">固定数据库上下文。</param>
    /// <param name="connection">事务连接。</param>
    /// <param name="transaction">事务对象。</param>
    /// <param name="lease">事务作用域执行租约。</param>
    void BindTransactionScope(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
        ISqlTransactionScopeLease lease);
}

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