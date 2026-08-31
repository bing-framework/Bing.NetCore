using Bing.Text;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 租户域名解析器辅助操作
/// </summary>
public class DomainTenantResolverHelper
{
    /// <summary>
    /// 移除域名前的 HTTP 或 HTTPS 协议前缀。
    /// </summary>
    /// <param name="domain">域名</param>
    /// <returns>移除开头 HTTP 或 HTTPS 协议前缀后的域名。</returns>
    public static string RemoveDomainPrefix(string domain) => domain.RemoveStart("http://").RemoveStart("https://");
}
