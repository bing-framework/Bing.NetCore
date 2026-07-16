using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域工厂
/// </summary>
public interface ISqlTransactionScopeFactory
{
    /// <summary>
    /// 开始 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <returns>SQL 事务作用域</returns>
    ISqlTransactionScope Begin(string dbKey = null);

    /// <summary>
    /// 使用指定隔离级别开始 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <returns>SQL 事务作用域</returns>
    ISqlTransactionScope Begin(string dbKey, IsolationLevel isolationLevel);

    /// <summary>
    /// 异步开始 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SQL 事务作用域</returns>
    Task<ISqlTransactionScope> BeginAsync(string dbKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定隔离级别异步开始 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SQL 事务作用域</returns>
    Task<ISqlTransactionScope> BeginAsync(string dbKey, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}