using Bing.MultiTenancy;
using Bing.Text;
using Bing.Text.Formatting;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于域名的租户解析构造器
/// </summary>
public class DomainTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 初始化一个<see cref="DomainTenantResolveContributor"/>类型的实例
    /// </summary>
    /// <param name="domainFormat">域名格式字符串。范例：{0}.a.com</param>
    public DomainTenantResolveContributor(string domainFormat)
    {
        DomainFormat = domainFormat;
    }

    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "Domain";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 域名格式字符串
    /// </summary>
    public string DomainFormat { get; }

    /// <summary>
    /// 从 <see cref="HttpContext"/> 中获取租户标识、租户名称、null
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <param name="httpContext">Http上下文</param>
    /// <returns>租户标识、租户名称、null</returns>
    protected override Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        if (!httpContext.Request.Host.HasValue)
            return Task.FromResult<string>(null);
        var hostName = DomainTenantResolverHelper.RemoveDomainPrefix(httpContext.Request.Host.Value);
        var extractResult = FormattedStringValueExtractor.Extract(hostName, DomainFormat, ignoreCase: true);
        context.Handled = true;
        return Task.FromResult(extractResult.IsMatch ? extractResult.Matches[0].Value : null);
    }
}
