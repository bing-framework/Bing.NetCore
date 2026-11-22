using Bing.DependencyInjection;

namespace Bing.Uow;

/// <summary>
/// 管理当前作用域中登记的工作单元。
/// </summary>
public interface IUnitOfWorkManager : IScopedDependency
{
    /// <summary>
    /// 提交当前管理器中登记的所有工作单元。
    /// </summary>
    void Commit();

    /// <summary>
    /// 异步提交当前管理器中登记的所有工作单元。
    /// </summary>
    /// <param name="cancellationToken">取消提交操作的令牌。</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将工作单元登记到当前管理器。
    /// </summary>
    /// <param name="unitOfWork">要登记的工作单元。</param>
    void Register(IUnitOfWork unitOfWork);

    /// <summary>
    /// 获取当前已登记的工作单元。
    /// </summary>
    /// <returns>当前管理器中的工作单元只读集合。</returns>
    IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks();
}
