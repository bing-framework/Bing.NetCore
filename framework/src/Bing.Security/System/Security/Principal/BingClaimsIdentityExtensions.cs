using System.Linq;
using System.Security.Claims;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Security.Claims;
using Bing.Text;

namespace System.Security.Principal
{
    /// <summary>
    /// 声明标识扩展
    /// </summary>
    public static class BingClaimsIdentityExtensions
    {
        #region FindUserId(查找用户标识)

        /// <summary>
        /// 查找用户标识
        /// </summary>
        /// <param name="principal">声明主体</param>
        public static Guid? FindUserId(this ClaimsPrincipal principal)
        {
            Check.NotNull(principal, nameof(principal));
            var userIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.UserId);
            if (userIdOrNull == null || userIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            if (Guid.TryParse(userIdOrNull.Value, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// 查找用户标识
        /// </summary>
        /// <param name="identity">标识</param>
        public static Guid? FindUserId(this IIdentity identity)
        {
            Check.NotNull(identity, nameof(identity));
            var claimsIdentity = identity as ClaimsIdentity;
            var userIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.UserId);
            if (userIdOrNull == null || userIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            return Guid.Parse(userIdOrNull.Value);
        }

        #endregion

        #region FindTenantId(查找租户标识)

        /// <summary>
        /// 查找用户标识
        /// </summary>
        /// <param name="principal">声明主体</param>
        public static Guid? FindTenantId(this ClaimsPrincipal principal)
        {
            Check.NotNull(principal, nameof(principal));
            var tenantIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.TenantId);
            if (tenantIdOrNull == null || tenantIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            if (Guid.TryParse(tenantIdOrNull.Value, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// 查找用户标识
        /// </summary>
        /// <param name="identity">标识</param>
        public static Guid? FindTenantId(this IIdentity identity)
        {
            Check.NotNull(identity, nameof(identity));
            var claimsIdentity = identity as ClaimsIdentity;
            var tenantIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.TenantId);
            if (tenantIdOrNull == null || tenantIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            return Guid.Parse(tenantIdOrNull.Value);
        }

        #endregion

        #region FindClientId(查找客户端标识)

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

        #endregion

        #region FindEditionId(查找版本标识)

        /// <summary>
        /// 查找版本标识
        /// </summary>
        /// <param name="principal">声明主体</param>
        public static Guid? FindEditionId(this ClaimsPrincipal principal)
        {
            Check.NotNull(principal, nameof(principal));
            var editionIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.EditionId);
            if (editionIdOrNull == null || editionIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            if (Guid.TryParse(editionIdOrNull.Value, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// 查找版本标识
        /// </summary>
        /// <param name="identity">标识</param>
        public static Guid? FindEditionId(this IIdentity identity)
        {
            Check.NotNull(identity, nameof(identity));
            var claimsIdentity = identity as ClaimsIdentity;
            var editionIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.EditionId);
            if (editionIdOrNull == null || editionIdOrNull.Value.IsNullOrWhiteSpace())
                return null;
            return Guid.Parse(editionIdOrNull.Value);
        }

        #endregion
    }
}
