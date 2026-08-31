using Bing.Collections;
using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 根据请求头解析租户标识的租户解析贡献者。
/// </summary>
public class HeaderTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 获取该租户解析贡献者使用的稳定名称。
    /// </summary>
    public const string ContributorName = "Header";

    /// <summary>
    /// 获取该租户解析贡献者的名称。
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 异步从请求头中提取租户标识或名称。
    /// </summary>
    /// <param name="context">租户解析上下文。</param>
    /// <param name="httpContext">当前 HTTP 请求上下文。</param>
    /// <returns>表示解析操作的任务，结果为第一个租户请求头值；请求头缺失或没有值时返回 <see langword="null"/>。</returns>
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
    /// 记录租户请求头存在多个值时的诊断日志。
    /// </summary>
    /// <param name="context">租户解析上下文。</param>
    /// <param name="text">日志内容。</param>
    protected virtual void Log(ITenantResolveContext context, string text)
    {
        context.ServiceProvider
            .GetRequiredService<ILogger<HeaderTenantResolveContributor>>()
            .LogWarning(text);
    }
}
