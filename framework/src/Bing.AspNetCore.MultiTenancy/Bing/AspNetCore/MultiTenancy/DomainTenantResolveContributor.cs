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
    /// 初始化一个 <see cref="DomainTenantResolveContributor"/> 类型的实例。
    /// </summary>
    /// <param name="domainFormat">租户域名格式，例如 <c>{0}.a.com</c>。</param>
    public DomainTenantResolveContributor(string domainFormat)
    {
        _domainFormat = DomainTenantResolverHelper.RemoveDomainPrefix(domainFormat);
    }

    /// <summary>
    /// 标识域名租户解析贡献者的稳定名称，值为 <c>Domain</c>。
    /// </summary>
    public const string ContributorName = "Domain";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <summary>
    /// 获取用于提取租户标识的域名格式。
    /// </summary>
    public string DomainFormat => _domainFormat;

    /// <inheritdoc />
    /// <remarks>
    /// 异步从请求域名中提取租户标识或名称。
    /// </remarks>
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
