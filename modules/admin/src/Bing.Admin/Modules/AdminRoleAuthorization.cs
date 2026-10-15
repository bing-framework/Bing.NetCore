using System;
using System.Linq;
using System.Security.Claims;

namespace Bing.Admin.Modules;

/// <summary>
/// 提供 Admin 运维角色匹配逻辑。
/// </summary>
internal static class AdminRoleAuthorization
{
    /// <summary>
    /// Admin 角色编码声明类型。
    /// </summary>
    private const string RoleCodeClaimType = "role_code";

    /// <summary>
    /// 判断已认证用户是否具备指定角色。
    /// </summary>
    /// <param name="principal">当前用户。</param>
    /// <param name="requiredRole">要求的角色编码。</param>
    /// <returns>用户已认证且角色匹配时返回 <see langword="true"/>。</returns>
    internal static bool HasRole(ClaimsPrincipal principal, string requiredRole)
    {
        if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(requiredRole))
            return false;

        return principal.Claims
            .Where(claim => claim.Type == RoleCodeClaimType || claim.Type == ClaimTypes.Role)
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(role => role.Trim())
            .Any(role => string.Equals(role, requiredRole.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
