using Bing.DependencyInjection;
using Bing.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 将租户解析结果存储在当前 HTTP 请求上下文中的访问器。
/// </summary>
[Dependency(ServiceLifetime.Transient, ReplaceExisting = true)]
public class HttpContextTenantResolveResultAccessor : ITenantResolveResultAccessor
{
    /// <summary>
    /// 在当前 <see cref="HttpContext.Items"/> 中保存租户解析结果的内部键。
    /// </summary>
    /// <remarks>同一请求链路通过此键复用同一个 <see cref="TenantResolveResult"/> 实例。</remarks>
    public const string HttpContextItemName = "__BingTenantResolveResult";

    /// <summary>
    /// 用于获取当前 HTTP 请求上下文的访问器。
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 使用指定 HTTP 上下文访问器初始化 <see cref="HttpContextTenantResolveResultAccessor"/> 的实例。
    /// </summary>
    /// <param name="httpContextAccessor">用于读取当前 HTTP 请求上下文的访问器。</param>
    public HttpContextTenantResolveResultAccessor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// 获取或设置当前 HTTP 请求缓存的租户解析结果；没有 HTTP 上下文时读取返回 <c>null</c>，写入被忽略。
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
