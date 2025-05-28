using System.Diagnostics;
using Bing.Helpers;
using Bing.Security.Claims;

namespace Bing.Users;

/// <summary>
/// 当前用户(<see cref="CurrentUser"/>) 扩展
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// 查找声明值
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <param name="claimType">声明类型</param>
    public static string FindClaimValue(this ICurrentUser currentUser, string claimType) => currentUser.FindClaim(claimType)?.Value;

    /// <summary>
    /// 查找声明值
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="currentUser">当前用户</param>
    /// <param name="claimType">声明类型</param>
    public static T FindClaimValue<T>(this ICurrentUser currentUser, string claimType)
        where T : struct
    {
        var value = currentUser.FindClaimValue(claimType);
        if (value == null)
            return default;
        return Conv.To<T>(value);
    }

    /// <summary>
    /// 获取用户标识
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static Guid GetUserId(this ICurrentUser currentUser) => Conv.ToGuid(currentUser.UserId);

    /// <summary>
    /// 获取用户标识
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="currentUser">当前用户</param>
    public static T GetUserId<T>(this ICurrentUser currentUser) => Conv.To<T>(currentUser.UserId);

    /// <summary>
    /// 获取用户名
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetUserName(this ICurrentUser currentUser)
    {
        var result = currentUser.FindClaim(BingClaimTypes.UserName)?.Value;
        if (string.IsNullOrWhiteSpace(result))
            result = currentUser.FindClaim("name")?.Value;
        return result;
    }

    /// <summary>
    /// 获取姓名
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetFullName(this ICurrentUser currentUser)
    {
        var result = currentUser.FindClaim(BingClaimTypes.FullName)?.Value;
        if (string.IsNullOrWhiteSpace(result))
            result = currentUser.FindClaim("family_name")?.Value;
        return result;
    }

    /// <summary>
    /// 获取电子邮件
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetEmail(this ICurrentUser currentUser)
    {
        var result = currentUser.FindClaim(BingClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(result))
            result = currentUser.FindClaim("email")?.Value;
        return result;
    }

    /// <summary>
    /// 获取手机号码
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetPhoneNumber(this ICurrentUser currentUser)
    {
        var result = currentUser.FindClaim(BingClaimTypes.PhoneNumber)?.Value;
        if (string.IsNullOrWhiteSpace(result))
            result = currentUser.FindClaim("phone_number")?.Value;
        return result;
    }

    /// <summary>
    /// 获取会话标识。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 返回当前用户的 <c>SessionId</c>（会话 ID）。
    /// </returns>
    public static string GetSessionId(this ICurrentUser currentUser)
    {
        var sessionId = currentUser.FindSessionId();
        Debug.Assert(sessionId != null, "session != null");
        return sessionId!;
    }

    /// <summary>
    /// 查找会话标识。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 返回当前用户的 <c>SessionId</c>（会话 ID）；
    /// 如果当前用户没有会话 ID，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 该方法从用户 Claims 中查找 <c>SessionId</c>，用于唯一标识用户会话。
    /// SessionId 可能由身份认证系统（如 JWT、OAuth2）生成，并存储在 Claims 中。<br />
    ///
    /// 典型应用场景：<br />
    /// - **获取当前会话 ID** 以便跟踪用户请求。<br />
    /// - **用于 API 鉴权**，确保每个请求都包含有效的会话标识符。<br />
    /// - **日志分析和审计**，用于标识不同用户的请求行为。
    /// </remarks>
    public static string FindSessionId(this ICurrentUser currentUser)
    {
        return currentUser.FindClaimValue(BingClaimTypes.SessionId);
    }

    #region Application(应用程序)

    /// <summary>
    /// 获取应用程序标识
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static Guid GetApplicationId(this ICurrentUser currentUser) => Conv.ToGuid(currentUser.FindClaim(BingClaimTypes.ApplicationId)?.Value);

    /// <summary>
    /// 获取应用程序标识
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="currentUser">当前用户</param>
    public static T GetApplicationId<T>(this ICurrentUser currentUser) => Conv.To<T>(currentUser.FindClaim(BingClaimTypes.ApplicationId)?.Value);

    /// <summary>
    /// 获取应用程序编码
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetApplicationCode(this ICurrentUser currentUser) => currentUser.FindClaim(BingClaimTypes.ApplicationCode)?.Value;

    /// <summary>
    /// 获取应用程序名称
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetApplicationName(this ICurrentUser currentUser) => currentUser.FindClaim(BingClaimTypes.ApplicationName)?.Value;

    #endregion

    #region Tenant(租户)

    /// <summary>
    /// 获取租户标识
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static Guid GetTenantId(this ICurrentUser currentUser) => Conv.ToGuid(currentUser.FindClaim(BingClaimTypes.TenantId)?.Value);

    /// <summary>
    /// 获取租户标识
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="currentUser">当前用户</param>
    public static T GetTenantId<T>(this ICurrentUser currentUser) => Conv.To<T>(currentUser.FindClaim(BingClaimTypes.TenantId)?.Value);

    /// <summary>
    /// 获取租户编码
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetTenantCode(this ICurrentUser currentUser) => currentUser.FindClaim(BingClaimTypes.TenantCode)?.Value;

    /// <summary>
    /// 获取租户名称
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string GetTenantName(this ICurrentUser currentUser) => currentUser.FindClaim(BingClaimTypes.TenantName)?.Value;

    #endregion

    #region Impersonator(模拟租户用户)

    /// <summary>
    /// 查找模拟租户标识。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 如果当前用户正在被模拟，则返回模拟租户的 <see cref="Guid"/> ID；
    /// 如果当前用户未被模拟或未找到对应的 Claim，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 在多租户系统中，超级管理员可以模拟租户管理员或其他用户的身份，
    /// 此方法用于检查当前用户是否处于模拟模式，并获取发起模拟的租户 ID。
    /// <br />
    /// 该方法通常用于：<br />
    /// - **日志记录**：记录模拟信息，防止越权行为。<br />
    /// - **UI 显示**：在前端标识 "您当前正在模拟租户 X"。<br />
    /// - **权限管理**：在某些 API 端点，阻止模拟用户执行某些敏感操作。
    /// </remarks>
    public static Guid? FindImpersonatorTenantId(this ICurrentUser currentUser)
    {
        var impersonatorTenantId = currentUser.FindClaimValue(BingClaimTypes.ImpersonatorTenantId);
        if (string.IsNullOrWhiteSpace(impersonatorTenantId))
            return null;
        if (Guid.TryParse(impersonatorTenantId, out var guid))
            return guid;
        return null;
    }

    /// <summary>
    /// 查找模拟用户标识。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 如果当前用户正在被模拟，则返回模拟用户的 <see cref="Guid"/> ID；
    /// 如果当前用户未被模拟或未找到对应的 Claim，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 身份模拟（Impersonation）允许某个用户模拟其他用户的身份并进行操作，
    /// 此方法用于检查当前用户是否被模拟，并返回模拟的用户 ID。
    /// </remarks>
    public static Guid? FindImpersonatorUserId(this ICurrentUser currentUser)
    {
        var impersonatorUserId = currentUser.FindClaimValue(BingClaimTypes.ImpersonatorUserId);
        if (string.IsNullOrWhiteSpace(impersonatorUserId))
            return null;
        if (Guid.TryParse(impersonatorUserId, out var guid))
            return guid;
        return null;
    }

    /// <summary>
    /// 查找模拟租户编码。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 返回模拟租户的代码。如果没有模拟租户，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 获取当前用户的模拟租户代码，通常用于标识用户当前操作的租户的代码。
    /// </remarks>
    public static string FindImpersonatorTenantCode(this ICurrentUser currentUser) => currentUser.FindClaimValue(BingClaimTypes.ImpersonatorTenantCode);

    /// <summary>
    /// 查找模拟租户名称。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 返回模拟租户的名称。如果没有模拟租户，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 获取当前用户的模拟租户名称，通常用于显示当前正在模拟的租户信息。
    /// </remarks>
    public static string FindImpersonatorTenantName(this ICurrentUser currentUser) => currentUser.FindClaimValue(BingClaimTypes.ImpersonatorTenantName);

    /// <summary>
    /// 查找模拟用户名称。
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <returns>
    /// 返回模拟用户的用户名。如果没有模拟用户，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>
    /// 获取当前用户的模拟用户名，通常用于标识当前正在模拟的用户的名称。
    /// </remarks>
    public static string FindImpersonatorUserName(this ICurrentUser currentUser) => currentUser.FindClaimValue(BingClaimTypes.ImpersonatorUserName);

    #endregion

    #region Role(角色)

    /// <summary>
    /// 获取角色标识列表
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static List<Guid> GetRoleIds(this ICurrentUser currentUser) => GetRoleIds<Guid>(currentUser);

    /// <summary>
    /// 获取角色标识列表
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="currentUser">当前用户</param>
    public static List<T> GetRoleIds<T>(this ICurrentUser currentUser)
    {
        var result = currentUser.FindClaimValue(BingClaimTypes.RoleIds);
        return Conv.ToList<T>(string.IsNullOrWhiteSpace(result)
            ? currentUser.FindClaimValue(BingClaimTypes.Role)
            : result);
    }

    /// <summary>
    /// 获取角色编码列表
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string[] GetRoleCodes(this ICurrentUser currentUser) => currentUser.FindClaims(BingClaimTypes.RoleCodes)?.Select(x => x.Value).ToArray();

    /// <summary>
    /// 获取角色名称列表
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    public static string[] GetRoleNames(this ICurrentUser currentUser) => currentUser.FindClaims(BingClaimTypes.RoleNames)?.Select(x => x.Value).ToArray();

    #endregion

}
