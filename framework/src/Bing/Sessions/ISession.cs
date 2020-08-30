using System;
using System.Security.Claims;

namespace Bing.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    [Obsolete("请使用ICurrentUser")]
    public interface ISession
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// 是否认证
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
    }
}
