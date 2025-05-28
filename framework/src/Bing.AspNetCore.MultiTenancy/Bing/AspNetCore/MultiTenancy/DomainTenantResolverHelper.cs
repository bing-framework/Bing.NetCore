using Bing.Text;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 租户域名解析器辅助操作
/// </summary>
public class DomainTenantResolverHelper
{
    /// <summary>
    /// 移除域名齐纳喝醉
    /// </summary>
    /// <param name="domain">域名</param>
    public static string RemoveDomainPrefix(string domain) => domain.RemoveStart("http://").RemoveStart("https://");
}
