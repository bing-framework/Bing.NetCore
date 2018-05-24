using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Bing.Security.Principals;
using Microsoft.AspNetCore.Http;

namespace Bing
{
    /// <summary>
    /// 安全扩展
    /// </summary>
    public static class SecurityExtensions
    {
        /// <summary>
        /// 获取用户标识声明值
        /// </summary>
        /// <param name="identity">用户标识</param>
        /// <param name="type">声明类型</param>
        /// <returns></returns>
        public static string GetValue(this ClaimsIdentity identity, string type)
        {
            var claim = identity.FindFirst(type);
            if (claim == null)
            {
                return string.Empty;
            }

            return claim.Value;
        }

        /// <summary>
        /// 获取身份标识
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <returns></returns>
        public static ClaimsIdentity GetIdentity(this HttpContext context)
        {
            if (context == null)
            {
                return UnauthenticatedIdentity.Instance;
            }

            if (!(context.User is ClaimsPrincipal principal))
            {
                return UnauthenticatedIdentity.Instance;
            }

            if (principal.Identity is ClaimsIdentity identity)
            {
                return identity;
            }

            return UnauthenticatedIdentity.Instance;
        }
    }
}
