using System;
using System.Collections.Generic;
using System.Linq;
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
    public static T FindClaimValue<T>(this ICurrentUser currentUser, string claimType) where T : struct
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