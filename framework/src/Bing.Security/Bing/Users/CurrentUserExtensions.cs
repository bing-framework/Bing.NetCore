using System;
using System.Diagnostics;
using Bing.Helpers;

namespace Bing.Users
{
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
        public static Guid GetUserId(this ICurrentUser currentUser)
        {
            Debug.Assert(currentUser.UserId != null, "currentUser.UserId != null");
            return Conv.ToGuid(currentUser.UserId);
        }
    }
}
