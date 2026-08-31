using Bing.Aspects;

namespace Bing.Uow;

/// <summary>
/// 工作单元
/// </summary>
//[Ignore]
[IgnoreAspect]
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 提交，返回影响的行数
    /// </summary>
    /// <returns>提交操作影响的行数。</returns>
    int Commit();

    /// <summary>
    /// 提交，返回影响的行数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步提交操作的任务，结果为影响的行数。</returns>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
