using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 从 HTTP Cookie 解析租户的贡献者。
/// </summary>
public class CookieTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 用于诊断和解析链路记录的贡献者名称。
    /// </summary>
    public const string ContributorName = "Cookie";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <inheritdoc />
    /// <remarks>使用当前租户键从请求 Cookie 读取租户标识；键不存在时返回 <c>null</c>。</remarks>
    protected override Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        var key = GetTenantKey(context);
        var tenantId = httpContext.Request.Cookies[key];
        return Task.FromResult(tenantId);
    }
}
