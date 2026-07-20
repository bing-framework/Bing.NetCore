using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务只读上下文。
/// </summary>
public interface ISqlTransactionContext
{
    /// <summary>
    /// 获取事务标识。
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 获取数据库标识。
    /// </summary>
    string DbKey { get; }

    /// <summary>
    /// 获取数据库类型。
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 获取固定的数据库上下文快照。
    /// </summary>
    DatabaseContext DatabaseContext { get; }

    /// <summary>
    /// 获取事务使用的数据库连接。
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// 获取事务使用的数据库事务。
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// 获取事务隔离级别。
    /// </summary>
    IsolationLevel IsolationLevel { get; }
}