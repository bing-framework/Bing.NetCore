using Bing.Uow;

namespace Bing.DependencyInjection;

/// <summary>
/// 配置显式启用的自动工作单元边界。
/// </summary>
public sealed class BingAutomaticUnitOfWorkOptions
{
    /// <summary>
    /// 获取或设置自动 owner 使用的默认工作单元选项。
    /// </summary>
    public UnitOfWorkScopeOptions DefaultOptions { get; set; } = new();
}
