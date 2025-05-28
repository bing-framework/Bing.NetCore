using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于Http实现的租户解析构造器基类
/// </summary>
public abstract class HttpTenantResolveContributorBase : TenantResolveContributorBase
{
    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
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
    /// 从 <see cref="HttpContext"/> 中解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <param name="httpContext">Http上下文</param>
    protected virtual async Task ResolveFromHttpContextAsync(ITenantResolveContext context, HttpContext httpContext)
    {
        var tenantIdOrName = await GetTenantIdOrNameFromHttpContextOrNullAsync(context, httpContext);
        if (!string.IsNullOrWhiteSpace(tenantIdOrName))
            context.TenantIdOrName = tenantIdOrName;
    }

    /// <summary>
    /// 从 <see cref="HttpContext"/> 中获取租户标识、租户名称、null
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <param name="httpContext">Http上下文</param>
    /// <returns>租户标识、租户名称、null</returns>
    protected abstract Task<string> GetTenantIdOrNameFromHttpContextOrNullAsync(ITenantResolveContext context, HttpContext httpContext);

    /// <summary>
    /// 获取租户键名
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    protected string GetTenantKey(ITenantResolveContext context)
    {
        var options = context.GetMultiTenancyOptions();
        return options.TenantKey;
    }
}
