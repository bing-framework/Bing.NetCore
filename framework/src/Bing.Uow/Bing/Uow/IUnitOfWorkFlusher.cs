namespace Bing.Uow;

/// <summary>
/// 在受管理的写入范围内显式保存待处理更改。
/// </summary>
/// <remarks>
/// 此扩展能力不改变 <see cref="IUnitOfWork.Commit"/> 的提交契约。
/// </remarks>
public interface IUnitOfWorkFlusher
{
    /// <summary>
    /// 保存写入并回填数据库生成的键。
    /// </summary>
    /// <returns>本次保存影响的行数。</returns>
    /// <remarks>此操作不提交当前事务，且必须处于受管理的写入范围。</remarks>
    int Flush();

    /// <summary>
    /// 异步保存写入并回填数据库生成的键。
    /// </summary>
    /// <param name="cancellationToken">取消保存操作的令牌。</param>
    /// <returns>本次保存影响的行数。</returns>
    /// <remarks>此操作不提交当前事务。</remarks>
    Task<int> FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在受管理事务范围内执行同步写入。
    /// </summary>
    /// <param name="action">要执行的写入操作。</param>
    /// <remarks>嵌套调用加入当前事务；最外层调用成功后完成提交。</remarks>
    void ExecuteInWriteScope(Action action);

    /// <summary>
    /// 在受管理事务范围内执行异步写入。
    /// </summary>
    /// <param name="action">接收取消令牌的异步写入操作。</param>
    /// <param name="cancellationToken">取消写入操作的令牌。</param>
    /// <remarks>操作失败时异常向调用方传播，当前事务不能提交。</remarks>
    Task ExecuteInWriteScopeAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
