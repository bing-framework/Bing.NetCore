using System.Security.Claims;
using Bing.Security.Principals;

// ReSharper disable once CheckNamespace
namespace Bing
{
    /// <summary>
    /// 安全扩展
    /// </summary>
    public static class SecurityExtensions
    {
        ///// <summary>
        ///// 获取身份标识
        ///// </summary>
        ///// <param name="context">Http上下文</param>
        //public static ClaimsIdentity GetIdentity(this HttpContext context)
        //{
        //    if (context == null)
        //        return UnauthenticatedIdentity.Instance;
        //    if (!(context.User is ClaimsPrincipal principal))
        //        return UnauthenticatedIdentity.Instance;
        //    if (principal.Identity is ClaimsIdentity identity)
        //        return identity;
        //    return UnauthenticatedIdentity.Instance;
        //}
    }
}
