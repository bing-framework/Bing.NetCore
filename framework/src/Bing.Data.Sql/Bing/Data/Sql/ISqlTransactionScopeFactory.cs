using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 创建 SQL 事务作用域。
/// </summary>
public interface ISqlTransactionScopeFactory
{
    /// <summary>
    /// 开始 SQL 事务作用域。
    /// </summary>
    /// <returns>新建的 SQL 事务作用域。</returns>
    ISqlTransactionScope Begin();

    /// <summary>
    /// 使用指定数据源开始 SQL 事务作用域。
    /// </summary>
    /// <param name="dataSourceKey">逻辑数据源标识。</param>
    /// <returns>新建的 SQL 事务作用域。</returns>
    ISqlTransactionScope Begin(string dataSourceKey);

    /// <summary>
    /// 使用指定隔离级别开始 SQL 事务作用域。
    /// </summary>
    /// <param name="dataSourceKey">逻辑数据源标识。</param>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <returns>新建的 SQL 事务作用域。</returns>
    ISqlTransactionScope Begin(string dataSourceKey, IsolationLevel isolationLevel);

    /// <summary>
    /// 异步开始 SQL 事务作用域。
    /// </summary>
    /// <param name="cancellationToken">用于取消创建操作的令牌。</param>
    /// <returns>最终返回新建 SQL 事务作用域的异步操作。</returns>
    Task<ISqlTransactionScope> BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定数据源异步开始 SQL 事务作用域。
    /// </summary>
    /// <param name="dataSourceKey">逻辑数据源标识。</param>
    /// <param name="cancellationToken">用于取消创建操作的令牌。</param>
    /// <returns>最终返回新建 SQL 事务作用域的异步操作。</returns>
    Task<ISqlTransactionScope> BeginAsync(string dataSourceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定隔离级别异步开始 SQL 事务作用域。
    /// </summary>
    /// <param name="dataSourceKey">逻辑数据源标识。</param>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <param name="cancellationToken">用于取消创建操作的令牌。</param>
    /// <returns>最终返回新建 SQL 事务作用域的异步操作。</returns>
    Task<ISqlTransactionScope> BeginAsync(string dataSourceKey, IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}
