using System.Security.Claims;

namespace Bing.Users
{
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
        /// 电子邮箱
        /// </summary>
        string Email { get; }

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
        Claim FindClaim(string claimType);

        /// <summary>
        /// 查找声明列表
        /// </summary>
        /// <param name="claimType">声明类型</param>
        Claim[] FindClaims(string claimType);

        /// <summary>
        /// 获取所有声明列表
        /// </summary>
        Claim[] GetAllClaims();

        /// <summary>
        /// 是否包含改角色
        /// </summary>
        /// <param name="roleName">角色名</param>
        bool IsInRole(string roleName);
    }
}
