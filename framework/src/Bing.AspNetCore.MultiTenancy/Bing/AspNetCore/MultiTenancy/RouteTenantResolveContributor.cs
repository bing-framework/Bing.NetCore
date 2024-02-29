﻿using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于路由的租户解析构造器
/// </summary>
public class RouteTenantResolveContributor : HttpTenantResolveContributorBase
{
    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "Route";

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
        var key = GetTenantKey(context);
        var tenantId = httpContext.GetRouteValue(key);
        return Task.FromResult(tenantId != null ? Convert.ToString(tenantId) : null);
    }
}