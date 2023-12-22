using Bing.Collections;
using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于请求头的租户解析构造器
/// </summary>
public class HeaderTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "Header";

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
        if (httpContext.Request.Headers.IsNullOrEmpty())
            return Task.FromResult<string>(null);
        var key = GetTenantKey(context);
        var tenantId = httpContext.Request.Headers[key];
        if (tenantId == string.Empty || tenantId.Count < 1)
            return Task.FromResult<string>(null);
        if (tenantId.Count > 1)
            Log(context, $"HTTP request includes more than one {key} header value. First one will be used. All of them: {tenantId.JoinToString(", ")}");
        return Task.FromResult(tenantId.First());
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <param name="text">日志内容</param>
    protected virtual void Log(ITenantResolveContext context, string text)
    {
        context.ServiceProvider
            .GetRequiredService<ILogger<HeaderTenantResolveContributor>>()
            .LogWarning(text);
    }
}
