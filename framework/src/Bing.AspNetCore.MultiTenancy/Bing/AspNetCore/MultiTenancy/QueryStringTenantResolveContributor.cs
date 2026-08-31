using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 从 HTTP 查询字符串解析租户的贡献者。
/// </summary>
public class QueryStringTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 用于诊断和解析链路记录的贡献者名称。
    /// </summary>
    public const string ContributorName = "QueryString";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <inheritdoc />
    /// <remarks>查询参数存在但值为空白时会终止后续解析；参数不存在时返回 <c>null</c> 以继续解析链。</remarks>
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
