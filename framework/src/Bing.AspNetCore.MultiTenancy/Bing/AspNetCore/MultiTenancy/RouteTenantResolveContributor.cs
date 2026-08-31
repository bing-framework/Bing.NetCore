using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 从 HTTP 路由值解析租户的贡献者。
/// </summary>
public class RouteTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 用于诊断和解析链路记录的贡献者名称。
    /// </summary>
    public const string ContributorName = "Route";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <inheritdoc />
    /// <remarks>使用当前租户键读取路由值；不存在路由值时返回 <c>null</c>。</remarks>
    protected override Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        var key = GetTenantKey(context);
        var tenantId = httpContext.GetRouteValue(key);
        return Task.FromResult(tenantId != null ? Convert.ToString(tenantId) : null);
    }
}
