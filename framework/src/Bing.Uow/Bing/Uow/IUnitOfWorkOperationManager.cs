namespace Bing.Uow;

/// <summary>
/// 管理可供业务操作使用的工作单元范围。
/// </summary>
public interface IUnitOfWorkOperationManager : IUnitOfWorkScopeManager
{
    /// <summary>
    /// 开始新的业务操作，或为当前 root 创建参与范围。
    /// </summary>
    /// <param name="options">工作单元选项；参与范围未指定时继承 root。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>带操作取消令牌的 root 或参与范围。</returns>
    IUnitOfWorkExecutionScope BeginOperation(UnitOfWorkScopeOptions options = null,
        CancellationToken cancellationToken = default);
}
