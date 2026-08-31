using System.Security.Claims;

namespace Bing.Users;

/// <summary>
/// 当前用户
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户标识
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// 手机号码
    /// </summary>
    string PhoneNumber { get; }

    /// <summary>
    /// 是否已验证手机号码
    /// </summary>
    bool PhoneNumberVerified { get; }

    /// <summary>
    /// 电子邮箱
    /// </summary>
    string Email { get; }

    /// <summary>
    /// 是否已验证邮箱
    /// </summary>
    bool EmailVerified { get; }

    /// <summary>
    /// 租户标识
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// 角色列表
    /// </summary>
    string[] Roles { get; }

    /// <summary>
    /// 查找声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>匹配的声明；未找到时返回 <see langword="null"/>。</returns>
    Claim FindClaim(string claimType);

    /// <summary>
    /// 查找声明列表
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>匹配的声明列表；未找到时返回空数组。</returns>
    Claim[] FindClaims(string claimType);

    /// <summary>
    /// 获取所有声明列表
    /// </summary>
    /// <returns>当前身份主体包含的全部声明；未设置身份主体时返回空数组。</returns>
    Claim[] GetAllClaims();

    /// <summary>
    /// 是否包含指定角色
    /// </summary>
    /// <param name="roleName">角色名</param>
    /// <returns>当前用户属于指定角色时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool IsInRole(string roleName);
}