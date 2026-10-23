namespace Bing.Uow;

/// <summary>
/// 在独立依赖注入范围和工作单元中执行一次业务操作。
/// </summary>
public interface IBingOperationExecutor
{
    /// <summary>
    /// 执行不返回结果的业务操作。
    /// </summary>
    /// <typeparam name="TService">由新依赖注入范围解析的业务服务类型。</typeparam>
    /// <param name="operation">业务操作委托。</param>
    /// <param name="options">工作单元选项。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    Task ExecuteAsync<TService>(Func<TService, CancellationToken, Task> operation,
        UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)
        where TService : class;

    /// <summary>
    /// 执行并返回已物化结果的业务操作。
    /// </summary>
    /// <typeparam name="TService">由新依赖注入范围解析的业务服务类型。</typeparam>
    /// <typeparam name="TResult">业务操作的结果类型。</typeparam>
    /// <param name="operation">业务操作委托。</param>
    /// <param name="options">工作单元选项。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>业务操作结果。</returns>
    Task<TResult> ExecuteAsync<TService, TResult>(Func<TService, CancellationToken, Task<TResult>> operation,
        UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)
        where TService : class;
}
