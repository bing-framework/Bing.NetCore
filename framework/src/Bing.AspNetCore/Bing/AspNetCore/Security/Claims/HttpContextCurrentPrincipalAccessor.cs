using System.Security.Claims;
using Bing.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Security.Claims;

/// <summary>
/// 当前Http上下文安全主体访问器
/// </summary>
public class HttpContextCurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor
{
    /// <summary>
    /// Http上下文访问器
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 初始化一个<see cref="HttpContextCurrentPrincipalAccessor"/>类型的实例
    /// </summary>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public HttpContextCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// 获取安全主体
    /// </summary>
    protected override ClaimsPrincipal GetClaimsPrincipal() => _httpContextAccessor.HttpContext?.User ?? base.GetClaimsPrincipal();
}