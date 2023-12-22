using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析上下文
/// </summary>
public interface ITenantResolveContext : IServiceProviderAccessor
{
    /// <summary>
    /// 租户ID或者租户名称
    /// </summary>
    string TenantIdOrName { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    bool Handled { get; set; }
}
