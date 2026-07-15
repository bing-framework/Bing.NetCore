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