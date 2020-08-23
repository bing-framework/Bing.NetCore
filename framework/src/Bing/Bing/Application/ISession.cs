using System.Security.Claims;
using Bing.Aspects;

namespace Bing.Application
{
    /// <summary>
    /// 会话
    /// </summary>
    [Ignore]
    public interface ISession
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// 用户名
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// 角色列表
        /// </summary>
        string[] Roles { get; }

        /// <summary>
        /// 是否已认证
        /// </summary>
        bool IsAuthenticated { get; }

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
        /// 是否包含指定角色
        /// </summary>
        /// <param name="roleName">角色名</param>
        bool IsInRole(string roleName);
    }
}
