using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.MultiTenancy;

/// <summary>
/// 多租户Cookie帮助类
/// </summary>
public static class BingMultiTenancyCookieHelper
{
    /// <summary>
    /// 设置租户Cookie
    /// </summary>
    /// <param name="context">Http上下文</param>
    /// <param name="tenantId">租户ID</param>
    /// <param name="tenantKey">租户键名</param>
    public static void SetTenantCookie(HttpContext context, string tenantId, string tenantKey)
    {
        if (tenantId != null)
        {
            context.Response.Cookies.Append(tenantKey, tenantId, new CookieOptions
            {
                Path = "/",
                HttpOnly = false,
                IsEssential = true,
                Expires = DateTimeOffset.Now.AddYears(10)
            });
        }
        else
        {
            context.Response.Cookies.Delete(tenantKey);
        }
    }
}
