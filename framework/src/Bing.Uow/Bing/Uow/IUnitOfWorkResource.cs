namespace Bing.Uow;

/// <summary>
/// 由业务工作单元统一完成、回滚和释放的单个数据源资源。
/// </summary>
public interface IUnitOfWorkResource : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 提交该资源的本地事务。
    /// </summary>
    void Commit();

    /// <summary>
    /// 异步提交该资源的本地事务。
    /// </summary>
    /// <param name="cancellationToken">提交取消令牌。</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚该资源的本地事务。
    /// </summary>
    void Rollback();

    /// <summary>
    /// 异步回滚该资源的本地事务。
    /// </summary>
    /// <param name="cancellationToken">回滚取消令牌；工作单元清理时使用不可取消令牌。</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
