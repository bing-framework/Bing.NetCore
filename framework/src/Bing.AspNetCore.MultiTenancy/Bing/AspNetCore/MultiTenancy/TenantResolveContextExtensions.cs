using Bing.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 租户解析上下文(<see cref="ITenantResolveContext"/>) 扩展
/// </summary>
public static class TenantResolveContextExtensions
{
    /// <summary>
    /// 获取多租户配置
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    public static MultiTenancyOptions GetMultiTenancyOptions(this ITenantResolveContext context) =>
        context.ServiceProvider.GetRequiredService<IOptions<MultiTenancyOptions>>().Value;
}
