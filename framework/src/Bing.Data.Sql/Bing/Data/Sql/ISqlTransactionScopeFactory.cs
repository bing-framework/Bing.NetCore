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
    /// 异步开始 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SQL 事务作用域</returns>
    Task<ISqlTransactionScope> BeginAsync(string dbKey = null, CancellationToken cancellationToken = default);
}