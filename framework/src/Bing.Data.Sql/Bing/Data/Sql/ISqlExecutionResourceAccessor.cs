using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 执行资源访问器。
/// </summary>
public interface ISqlQueryExecutionResourceAccessor
{
    /// <summary>
    /// 获取或创建执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    IDbConnection GetOrCreateConnection();

    /// <summary>
    /// 获取当前执行事务。
    /// </summary>
    /// <returns>当前事务，不存在时返回 null。</returns>
    IDbTransaction GetCurrentTransaction();

    /// <summary>
    /// 获取当前事务标识。
    /// </summary>
    /// <returns>当前事务标识，不存在时返回 null。</returns>
    string GetCurrentTransactionId();
}