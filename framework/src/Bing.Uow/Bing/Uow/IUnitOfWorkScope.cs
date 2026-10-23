namespace Bing.Uow;

/// <summary>
/// 表示可登记多个独立资源的业务工作单元范围。
/// </summary>
public interface IUnitOfWorkScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 获取 root 工作单元标识；参与式嵌套返回相同标识。
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 获取工作单元选项的副本。
    /// </summary>
    UnitOfWorkScopeOptions Options { get; }

    /// <summary>
    /// 获取 root 是否已成功完成所有资源提交。
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// 获取该范围是否仍可接受资源和完成回调。
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 获取 root 是否已被标记为仅允许回滚。
    /// </summary>
    bool IsRollbackOnly { get; }

    /// <summary>
    /// 获取该范围是否已释放。
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// 按不透明资源键获取已有资源，或登记新资源。
    /// </summary>
    /// <typeparam name="TResource">资源类型。</typeparam>
    /// <param name="key">非空且仅在当前 root 内有意义的资源键。</param>
    /// <param name="factory">创建尚未执行 I/O 的资源。</param>
    /// <returns>当前 root 中唯一登记的资源。</returns>
    TResource GetOrAddResource<TResource>(object key, Func<TResource> factory)
        where TResource : class, IUnitOfWorkResource;

    /// <summary>
    /// 为已登记资源获取非阻塞的独占执行租约。
    /// </summary>
    /// <param name="resourceKey">已登记资源的键。</param>
    /// <returns>释放时归还资源租约的句柄。</returns>
    IDisposable AcquireExecutionLease(object resourceKey);

    /// <summary>
    /// 登记仅在该资源提交确认后运行的成功回调。
    /// </summary>
    /// <param name="resourceKey">已登记资源的键。</param>
    /// <param name="callback">异步成功回调。</param>
    void OnResourceCommitted(object resourceKey, Func<Task> callback);

    /// <summary>
    /// 登记仅在所有资源提交成功后运行的全局回调。
    /// </summary>
    /// <param name="callback">异步成功回调。</param>
    void OnCompleted(Func<Task> callback);

    /// <summary>
    /// 将 root 标记为只能回滚。
    /// </summary>
    void SetRollbackOnly();

    /// <summary>
    /// 按登记顺序同步提交所有资源。
    /// </summary>
    void Complete();

    /// <summary>
    /// 按登记顺序异步提交所有资源。
    /// </summary>
    /// <param name="cancellationToken">业务提交取消令牌。</param>
    Task CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步回滚所有尚未提交的资源。
    /// </summary>
    void Rollback();

    /// <summary>
    /// 异步回滚所有尚未提交的资源。
    /// </summary>
    /// <param name="cancellationToken">保留的调用方令牌；资源清理本身不可取消。</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
