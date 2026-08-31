using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 在租户解析贡献者之间传递解析状态和候选租户信息。
/// </summary>
public interface ITenantResolveContext : IServiceProviderAccessor
{
    /// <summary>
    /// 获取或设置当前贡献者解析出的候选租户标识或名称；尚未解析时为 <c>null</c>。
    /// </summary>
    string? TenantIdOrName { get; set; }

    /// <summary>
    /// 获取或设置当前解析链是否已完成处理；设为 <c>true</c> 后不应继续执行后续贡献者。
    /// </summary>
    bool Handled { get; set; }
}
