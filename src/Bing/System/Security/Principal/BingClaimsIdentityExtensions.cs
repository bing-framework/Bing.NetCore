using System.Linq;
using System.Security.Claims;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Security.Claims;

namespace System.Security.Principal
{
    /// <summary>
    /// 声明标识扩展
    /// </summary>
    public static class BingClaimsIdentityExtensions
    {
        /// <summary>
        /// 查找客户端标识
        /// </summary>
        /// <param name="principal">声明主体</param>
        public static string FindClientId(this ClaimsPrincipal principal)
        {
            Check.NotNull(principal, nameof(principal));
            var clientIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ClientId);
            if (clientIdOrNull == null || clientIdOrNull.Value.IsEmpty())
                return null;
            return clientIdOrNull.Value;
        }

        /// <summary>
        /// 查找客户端标识
        /// </summary>
        /// <param name="identity">标识</param>
        public static string FindClientId(this IIdentity identity)
        {
            Check.NotNull(identity, nameof(identity));
            var claimsIdentity = identity as ClaimsIdentity;
            var clientIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ClientId);
            if (clientIdOrNull == null || clientIdOrNull.Value.IsEmpty())
                return null;
            return clientIdOrNull.Value;
        }
    }
}
