namespace Bing.Uow;

/// <summary>
/// 在工作单元物理提交前保存一个 ORM 的待处理更改。
/// </summary>
public interface IUnitOfWorkSaveChangesHandler
{
    /// <summary>
    /// 同步保存当前 root 中的待处理更改。
    /// </summary>
    /// <param name="scope">正在完成的工作单元范围。</param>
    void SaveChanges(IUnitOfWorkScope scope);

    /// <summary>
    /// 异步保存当前 root 中的待处理更改。
    /// </summary>
    /// <param name="scope">正在完成的工作单元范围。</param>
    /// <param name="cancellationToken">有效的操作取消令牌。</param>
    Task SaveChangesAsync(IUnitOfWorkScope scope, CancellationToken cancellationToken = default);
}
