using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于查询字符串的租户解析构造器
/// </summary>
public class QueryStringTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "QueryString";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 从 <see cref="HttpContext"/> 中获取租户标识、租户名称、null
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <param name="httpContext">Http上下文</param>
    /// <returns>租户标识、租户名称、null</returns>
    protected override Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        if (httpContext.Request.QueryString.HasValue)
        {
            var key = GetTenantKey(context);
            if (httpContext.Request.Query.ContainsKey(key))
            {
                var tenantId = httpContext.Request.Query[key].ToString();
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    context.Handled = true;
                    return Task.FromResult<string>(null);
                }

                return Task.FromResult(tenantId);
            }
        }

        return Task.FromResult<string>(null);
    }
}
