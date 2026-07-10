namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域工厂
/// </summary>
public interface ISqlTransactionScopeFactory
{
    /// <summary>
    /// 创建 SQL 事务作用域
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <returns>SQL 事务作用域</returns>
    ISqlTransactionScope Create(string dbKey = null);
}