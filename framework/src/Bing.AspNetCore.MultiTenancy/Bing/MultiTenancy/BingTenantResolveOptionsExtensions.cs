using Bing.AspNetCore.MultiTenancy;
using Bing.Collections;

namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析选项配置(<see cref="BingTenantResolveOptions" />) 扩展
/// </summary>
public static class BingTenantResolveOptionsExtensions
{
    /// <summary>
    /// 添加基于域名的租户解析器，在当前用户租户解析器之后插入。
    /// </summary>
    /// <param name="options">用户解析选项</param>
    /// <param name="domainFormat">用于解析租户的域名格式</param>
    public static void AddDomainTenantResolver(this BingTenantResolveOptions options, string domainFormat)
    {
        options.TenantResolvers.InsertAfter(
            r => r is CurrentUserTenantResolveContributor,
            new DomainTenantResolveContributor(domainFormat));
    }
}
