namespace Bing.Uow;

/// <summary>
/// 管理当前异步执行上下文中的业务工作单元范围。
/// </summary>
public interface IUnitOfWorkScopeManager
{
    /// <summary>
    /// 获取当前 root 工作单元；没有活动或继承的工作单元时返回 <see langword="null"/>。
    /// </summary>
    IUnitOfWorkScope Current { get; }

    /// <summary>
    /// 开始新的工作单元，或在当前 root 中创建参与式嵌套范围。
    /// </summary>
    /// <param name="options">工作单元选项；参与式嵌套未提供时继承 root 选项。</param>
    /// <param name="requiresNew">是否暂挂当前 root 并创建独立 root。</param>
    /// <returns>新 root 或参与式范围。</returns>
    IUnitOfWorkScope Begin(UnitOfWorkScopeOptions options = null, bool requiresNew = false);
}
