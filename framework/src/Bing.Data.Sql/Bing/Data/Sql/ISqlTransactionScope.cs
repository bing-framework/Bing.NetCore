namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务作用域
/// </summary>
public interface ISqlTransactionScope : ISqlTransactionContext, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 是否已完成提交或回滚
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 创建 SQL 查询对象
    /// </summary>
    ISqlQuery CreateQuery();

    /// <summary>
    /// 创建 SQL 执行器
    /// </summary>
    ISqlExecutor CreateExecutor();

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