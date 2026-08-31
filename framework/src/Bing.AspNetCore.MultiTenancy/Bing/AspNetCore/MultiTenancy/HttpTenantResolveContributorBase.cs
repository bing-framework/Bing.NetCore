using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 为基于 HTTP 请求来源的租户解析贡献者提供公共流程。
/// </summary>
public abstract class HttpTenantResolveContributorBase : TenantResolveContributorBase
{
    /// <inheritdoc />
    /// <remarks>当前实现仅在存在 HTTP 上下文时解析租户，并记录解析过程中的异常而不向上游传播。</remarks>
    public override async Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
            return;
        try
        {
            await ResolveFromHttpContextAsync(context, httpContext);
        }
        catch (Exception e)
        {
            context.ServiceProvider
                .GetRequiredService<ILogger<HttpTenantResolveContributorBase>>()
                .LogWarning(e.ToString());
        }
    }

    /// <summary>
    /// 从 HTTP 上下文解析候选租户并写入租户解析上下文。
    /// </summary>
    /// <param name="context">要写入候选租户的解析上下文。</param>
    /// <param name="httpContext">当前 HTTP 请求上下文。</param>
    protected virtual async Task ResolveFromHttpContextAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        var tenantIdOrName = await GetTenantIdOrNameFromHttpContextOrNullAsync(context, httpContext);
        if (!string.IsNullOrWhiteSpace(tenantIdOrName))
            context.TenantIdOrName = tenantIdOrName;
    }

    /// <summary>
    /// 从 HTTP 上下文的特定来源获取候选租户标识或名称。
    /// </summary>
    /// <param name="context">可由实现读取或更新的租户解析上下文。</param>
    /// <param name="httpContext">包含请求 Cookie、查询参数、路由值等信息的 HTTP 上下文。</param>
    /// <returns>解析出的候选租户标识或名称；当前来源未提供租户信息时返回 <c>null</c>。</returns>
    protected abstract Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext);

    /// <summary>
    /// 获取当前解析上下文配置的租户键名。
    /// </summary>
    /// <param name="context">用于获取多租户选项的租户解析上下文。</param>
    /// <returns>当前解析过程使用的租户键名。</returns>
    protected string GetTenantKey(ITenantResolveContext context)
    {
        var options = context.GetMultiTenancyOptions();
        return options.TenantKey;
    }
}
