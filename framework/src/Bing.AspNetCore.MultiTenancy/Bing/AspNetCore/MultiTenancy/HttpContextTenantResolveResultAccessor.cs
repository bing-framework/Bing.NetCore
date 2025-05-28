using Bing.DependencyInjection;
using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 基于Http上下文实现的租户解析结果访问器
/// </summary>
[Dependency(ServiceLifetime.Transient, ReplaceExisting = true)]
public class HttpContextTenantResolveResultAccessor : ITenantResolveResultAccessor
{
    /// <summary>
    /// Http上下文项名称
    /// </summary>
    public const string HttpContextItemName = "__BingTenantResolveResult";

    /// <summary>
    /// Http上下文访问器
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 初始化一个<see cref="HttpContextTenantResolveResultAccessor"/>类型的实例
    /// </summary>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public HttpContextTenantResolveResultAccessor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// 租户解析结果
    /// </summary>
    public TenantResolveResult Result
    {
        get => _httpContextAccessor.HttpContext?.Items[HttpContextItemName] as TenantResolveResult;
        set
        {
            if (_httpContextAccessor.HttpContext == null)
                return;
            _httpContextAccessor.HttpContext.Items[HttpContextItemName] = value;
        }
    }
}
