using System.Security.Claims;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Security.Claims;
using Bing.Text;

namespace System.Security.Principal;

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
        if (Guid.TryParse(userIdOrNull.Value, out var guid))
            return guid;
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
        if (Guid.TryParse(userIdOrNull.Value, out var guid))
            return guid;
        return null;
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
        if (Guid.TryParse(tenantIdOrNull.Value, out var guid))
            return guid;
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
        if (Guid.TryParse(tenantIdOrNull.Value, out var guid))
            return guid;
        return null;
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
        if (Guid.TryParse(editionIdOrNull.Value, out var guid))
            return guid;
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
        if (Guid.TryParse(editionIdOrNull.Value, out var guid))
            return guid;
        return null;
    }

    #endregion

    #region FindImpersonatorTenantId(查找模拟租户标识)

    /// <summary>
    /// 查找模拟租户标识
    /// </summary>
    /// <param name="principal">声明主体</param>
    public static Guid? FindImpersonatorTenantId(this ClaimsPrincipal principal)
    {
        Check.NotNull(principal, nameof(principal));
        var impersonatorTenantIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ImpersonatorTenantId);
        if (impersonatorTenantIdOrNull == null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace())
            return null;
        if (Guid.TryParse(impersonatorTenantIdOrNull.Value, out var guid))
            return guid;
        return null;
    }

    /// <summary>
    /// 查找模拟租户标识
    /// </summary>
    /// <param name="identity">标识</param>
    public static Guid? FindImpersonatorTenantId(this IIdentity identity)
    {
        Check.NotNull(identity, nameof(identity));
        var claimsIdentity = identity as ClaimsIdentity;
        var impersonatorTenantIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ImpersonatorTenantId);
        if (impersonatorTenantIdOrNull == null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace())
            return null;
        if (Guid.TryParse(impersonatorTenantIdOrNull.Value, out var guid))
            return guid;
        return null;
    }

    #endregion

    #region FindImpersonatorUserId(查找模拟用户标识)

    /// <summary>
    /// 查找模拟用户标识
    /// </summary>
    /// <param name="principal">声明主体</param>
    public static Guid? FindImpersonatorUserId(this ClaimsPrincipal principal)
    {
        Check.NotNull(principal, nameof(principal));
        var impersonatorUserIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ImpersonatorUserId);
        if (impersonatorUserIdOrNull == null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace())
            return null;
        if (Guid.TryParse(impersonatorUserIdOrNull.Value, out var guid))
            return guid;
        return null;
    }

    /// <summary>
    /// 查找模拟用户标识
    /// </summary>
    /// <param name="identity">标识</param>
    public static Guid? FindImpersonatorUserId(this IIdentity identity)
    {
        Check.NotNull(identity, nameof(identity));
        var claimsIdentity = identity as ClaimsIdentity;
        var impersonatorUserIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ImpersonatorUserId);
        if (impersonatorUserIdOrNull == null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace())
            return null;
        if (Guid.TryParse(impersonatorUserIdOrNull.Value, out var guid))
            return guid;
        return null;
    }

    #endregion

    #region FindSessionId(查找会话标识)

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="principal">声明主体</param>
    public static string FindSessionId(this ClaimsPrincipal principal)
    {
        Check.NotNull(principal, nameof(principal));
        var sessionIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ClientId);
        if (sessionIdOrNull == null || sessionIdOrNull.Value.IsEmpty())
            return null;
        return sessionIdOrNull.Value;
    }

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="identity">标识</param>
    public static string FindSessionId(this IIdentity identity)
    {
        Check.NotNull(identity, nameof(identity));
        var claimsIdentity = identity as ClaimsIdentity;
        var sessionIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == BingClaimTypes.ClientId);
        if (sessionIdOrNull == null || sessionIdOrNull.Value.IsEmpty())
            return null;
        return sessionIdOrNull.Value;
    }

    #endregion

    #region Add(添加)

    /// <summary>
    /// 添加。如果不存在指定声明，则进行添加
    /// </summary>
    /// <param name="claimsIdentity">声明标识</param>
    /// <param name="claim">声明</param>
    public static ClaimsIdentity AddIfNotContains(this ClaimsIdentity claimsIdentity, Claim claim)
    {
        Check.NotNull(claimsIdentity, nameof(claimsIdentity));
        if (!claimsIdentity.Claims.Any(x => string.Equals(x.Type, claim.Type, StringComparison.OrdinalIgnoreCase)))
            claimsIdentity.AddClaim(claim);
        return claimsIdentity;
    }

    /// <summary>
    /// 添加或替换。如果指定声明不存在直接添加，否则进行替换
    /// </summary>
    /// <param name="claimsIdentity">声明标识</param>
    /// <param name="claim">声明</param>
    public static ClaimsIdentity AddOrReplace(this ClaimsIdentity claimsIdentity, Claim claim)
    {
        Check.NotNull(claimsIdentity, nameof(claimsIdentity));
        foreach (var x in claimsIdentity.FindAll(claim.Type).ToList())
            claimsIdentity.RemoveClaim(x);
        claimsIdentity.AddClaim(claim);
        return claimsIdentity;
    }

    /// <summary>
    /// 添加。如果不存在指定声明标识，则进行添加
    /// </summary>
    /// <param name="principal">声明主体</param>
    /// <param name="identity">声明标识</param>
    public static ClaimsPrincipal AddIdentityIfNotContains(this ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        Check.NotNull(principal, nameof(principal));
        if (!principal.Identities.Any(x => string.Equals(x.AuthenticationType, identity.AuthenticationType, StringComparison.OrdinalIgnoreCase)))
            principal.AddIdentity(identity);
        return principal;
    }

    #endregion

    #region RemoveAll(移除全部)

    /// <summary>
    /// 移除全部，从 <see cref="ClaimsIdentity"/> 中移除所有指定类型的声明（Claims）。
    /// </summary>
    /// <param name="claimsIdentity">声明标识</param>
    /// <param name="claimType">声明类型</param>
    /// <returns>返回移除指定类型的声明后的 <see cref="ClaimsIdentity"/> 对象。</returns>
    /// <exception cref="ArgumentNullException">
    /// 如果 <paramref name="claimsIdentity"/> 为 <c>null</c>，则抛出异常。
    /// </exception>
    public static ClaimsIdentity RemoveAll(this ClaimsIdentity claimsIdentity, string claimType)
    {
        Check.NotNull(claimsIdentity, nameof(claimsIdentity));
        foreach (var x in claimsIdentity.FindAll(claimType).ToList())
            claimsIdentity.RemoveClaim(x);
        return claimsIdentity;
    }

    #endregion
}
