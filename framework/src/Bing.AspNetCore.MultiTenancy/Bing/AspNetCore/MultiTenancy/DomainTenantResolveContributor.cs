using Bing.MultiTenancy;
using Bing.Text.Formatting;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 根据请求域名格式解析租户标识的租户解析贡献者。
/// </summary>
public class DomainTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 保存去除域名前缀后的租户域名匹配格式。
    /// </summary>
    private readonly string _domainFormat;

    /// <summary>
    /// 初始化一个 <see cref="DomainTenantResolveContributor"/> 实例。
    /// </summary>
    /// <param name="domainFormat">租户域名格式，例如 <c>{0}.a.com</c>。</param>
    public DomainTenantResolveContributor(string domainFormat)
    {
        _domainFormat = DomainTenantResolverHelper.RemoveDomainPrefix(domainFormat);
    }

    /// <summary>
    /// 获取该租户解析贡献者的稳定名称。
    /// </summary>
    public const string ContributorName = "Domain";

    /// <summary>
    /// 获取该租户解析贡献者的名称。
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 获取用于提取租户标识的域名格式。
    /// </summary>
    public string DomainFormat => _domainFormat;

    /// <summary>
    /// 异步从请求域名中提取租户标识或名称。
    /// </summary>
    /// <param name="context">租户解析上下文。</param>
    /// <param name="httpContext">当前 HTTP 请求上下文。</param>
    /// <returns>表示解析操作的任务，结果为提取到的租户标识或名称；主机不存在或格式不匹配时返回 <see langword="null"/>。</returns>
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
