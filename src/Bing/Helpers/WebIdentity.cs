using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Bing.Security.Principals;
using Bing.Utils.Helpers;

namespace Bing.Helpers
{
    /// <summary>
    /// Web身份操作
    /// </summary>
    public static class WebIdentity
    {
        /// <summary>
        /// 当前用户安全主体
        /// </summary>
        public static ClaimsPrincipal User
        {
            get
            {
                if (Web.HttpContext == null)
                {
                    return UnauthenticatedPrincipal.Instance;
                }

                if (Web.HttpContext.User is ClaimsPrincipal principal)
                {
                    return principal;
                }
                return UnauthenticatedPrincipal.Instance;
            }
        }

        /// <summary>
        /// 当前用户身份
        /// </summary>
        public static ClaimsIdentity Identity
        {
            get
            {
                if (User.Identity is ClaimsIdentity identity)
                {
                    return identity;
                }

                return UnauthenticatedIdentity.Instance;
            }
        }
    }
}
