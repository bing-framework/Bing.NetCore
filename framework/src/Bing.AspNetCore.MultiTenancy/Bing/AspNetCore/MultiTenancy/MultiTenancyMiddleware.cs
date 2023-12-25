using Bing.DependencyInjection;
using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 多租户中间件
/// </summary>
public class MultiTenancyMiddleware : Microsoft.AspNetCore.Http.IMiddleware, ITransientDependency
{
    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<MultiTenancyMiddleware> Logger { get; set; }

    /// <summary>
    /// 租户配置提供程序
    /// </summary>
    private readonly ITenantConfigurationProvider _tenantConfigurationProvider;

    /// <summary>
    /// 当前租户
    /// </summary>
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 多租户配置
    /// </summary>
    private readonly MultiTenancyOptions _options;

    /// <summary>
    /// 租户解析结果访问器
    /// </summary>
    private readonly ITenantResolveResultAccessor _tenantResolveResultAccessor;

    /// <summary>
    /// 初始化一个<see cref="MultiTenancyMiddleware"/>类型的实例
    /// </summary>
    /// <param name="tenantConfigurationProvider">租户配置提供程序</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="options">多租户配置</param>
    /// <param name="tenantResolveResultAccessor">租户解析结果访问器</param>
    public MultiTenancyMiddleware(
        ITenantConfigurationProvider tenantConfigurationProvider,
        ICurrentTenant currentTenant,
        IOptions<MultiTenancyOptions> options,
        ITenantResolveResultAccessor tenantResolveResultAccessor)
    {
        Logger = NullLogger<MultiTenancyMiddleware>.Instance;
        _tenantConfigurationProvider = tenantConfigurationProvider;
        _currentTenant = currentTenant;
        _tenantResolveResultAccessor = tenantResolveResultAccessor;
        _options = options.Value;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        TenantConfiguration tenant = null;
        try
        {
            tenant = await _tenantConfigurationProvider.GetAsync(saveResolveResult: true);
        }
        catch (Exception e)
        {
            Logger.LogException(e);
            throw;
        }

        if (tenant?.Id != _currentTenant.Id)
        {
            using (_currentTenant.Change(tenant?.Id, tenant?.Name))
            {
                if (_tenantResolveResultAccessor.Result != null &&
                    _tenantResolveResultAccessor.Result.AppliedResolvers.Contains(QueryStringTenantResolveContributor.ContributorName))
                {
                    BingMultiTenancyCookieHelper.SetTenantCookie(context, _currentTenant.Id, _options.TenantKey);
                }

                await next(context);
            }
        }
        else
        {
            await next(context);
        }
    }
}
