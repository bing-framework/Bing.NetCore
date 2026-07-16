using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域
/// </summary>
public interface ISqlTransactionScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 事务标识
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 数据库键
    /// </summary>
    string DbKey { get; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 当前事务隔离级别
    /// </summary>
    IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// 作用域拥有的数据库连接
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// 作用域拥有的数据库事务
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// 是否已完成提交或回滚
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 创建 SQL 查询对象
    /// </summary>
    ISqlQuery CreateQuery();

    /// <summary>
    /// 创建 SQL 查询对象
    /// </summary>
    /// <typeparam name="TQuery">SQL 查询对象类型</typeparam>
    TQuery CreateQuery<TQuery>() where TQuery : class, ISqlQuery;

    /// <summary>
    /// 创建 SQL 执行器
    /// </summary>
    ISqlExecutor CreateExecutor();

    /// <summary>
    /// 创建 SQL 执行器
    /// </summary>
    /// <typeparam name="TExecutor">SQL 执行器类型</typeparam>
    TExecutor CreateExecutor<TExecutor>() where TExecutor : class, ISqlExecutor;

    /// <summary>
    /// 提交事务
    /// </summary>
    void Commit();

    /// <summary>
    /// 异步提交事务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚事务
    /// </summary>
    void Rollback();

    /// <summary>
    /// 异步回滚事务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}