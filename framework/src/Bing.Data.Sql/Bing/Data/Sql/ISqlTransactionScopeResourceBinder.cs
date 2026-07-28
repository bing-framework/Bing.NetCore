using System.Data;

namespace Bing.Data.Sql;

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