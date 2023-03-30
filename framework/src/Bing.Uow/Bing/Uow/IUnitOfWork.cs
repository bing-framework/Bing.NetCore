using Bing.Aspects;

namespace Bing.Uow;

/// <summary>
/// 工作单元
/// </summary>
[Ignore]
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 提交，返回影响的行数
    /// </summary>
    int Commit();

    /// <summary>
    /// 提交，返回影响的行数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
