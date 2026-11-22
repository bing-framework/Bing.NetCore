using Bing.Aspects;

namespace Bing.Uow;

/// <summary>
/// 保存当前业务操作中的更改并返回受影响的行数。
/// </summary>
//[Ignore]
[IgnoreAspect]
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 保存当前工作单元中的更改。
    /// </summary>
    /// <returns>提交操作影响的行数。</returns>
    int Commit();

    /// <summary>
    /// 异步保存当前工作单元中的更改。
    /// </summary>
    /// <param name="cancellationToken">取消保存操作的令牌。</param>
    /// <returns>表示异步提交操作的任务，结果为影响的行数。</returns>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
