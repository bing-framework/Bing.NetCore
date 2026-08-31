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
    /// <returns>绑定到当前事务作用域的 SQL 查询对象。</returns>
    ISqlQuery CreateQuery();

    /// <summary>
    /// 创建 SQL 执行器
    /// </summary>
    /// <returns>绑定到当前事务作用域的 SQL 执行器。</returns>
    ISqlExecutor CreateExecutor();

    /// <summary>
    /// 提交事务
    /// </summary>
    void Commit();

    /// <summary>
    /// 异步提交事务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚事务
    /// </summary>
    void Rollback();

    /// <summary>
    /// 异步回滚事务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}