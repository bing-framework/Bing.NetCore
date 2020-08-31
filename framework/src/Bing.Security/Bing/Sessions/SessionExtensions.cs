using System;
using System.Collections.Generic;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Security.Claims;

namespace Bing.Sessions
{
    /// <summary>
    /// 会话(<see cref="ISession"/>) 扩展
    /// </summary>
    public static partial class SessionExtensions
    {
        /// <summary>
        /// 查找声明值
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="claimType">声明类型</param>
        public static string FindClaimValue(this ISession session, string claimType) => session.FindClaim(claimType)?.Value;

        /// <summary>
        /// 查找声明值
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="session">会话</param>
        /// <param name="claimType">声明类型</param>
        public static T FindClaimValue<T>(this ISession session, string claimType) where T : struct
        {
            var value = session.FindClaimValue(claimType);
            if (value == null)
                return default;
            return Conv.To<T>(value);
        }

        /// <summary>
        /// 获取当前操作人标识
        /// </summary>
        /// <param name="session">会话</param>
        public static Guid GetUserId(this ISession session) => session.UserId.ToGuid();

        /// <summary>
        /// 获取当前操作人标识
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="session">会话</param>
        public static T GetUserTd<T>(this ISession session) => Conv.To<T>(session.UserId);

        /// <summary>
        /// 获取当前操作人用户名
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetUserName(this ISession session)
        {
            var result = session.FindClaimValue("name");// JwtClaimTypes.Name
            return string.IsNullOrWhiteSpace(result)
                ? session.FindClaimValue(System.Security.Claims.ClaimTypes.Name)
                : result;
        }

        /// <summary>
        /// 获取当前操作人姓名
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetFullName(this ISession session)
        {
            var result = session.FindClaimValue(ClaimTypes.FullName);
            return string.IsNullOrWhiteSpace(result)
                ? session.FindClaimValue(System.Security.Claims.ClaimTypes.Surname)
                : result;
        }

        /// <summary>
        /// 获取当前操作人电子邮件
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetEmail(this ISession session)
        {
            var result = session.FindClaimValue(ClaimTypes.Email);
            return string.IsNullOrWhiteSpace(result)
                ? session.FindClaimValue(System.Security.Claims.ClaimTypes.Email)
                : result;
        }

        /// <summary>
        /// 获取当前操作人手机号
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetMobile(this ISession session)
        {
            var result = session.FindClaimValue(ClaimTypes.Mobile);
            return string.IsNullOrWhiteSpace(result)
                ? session.FindClaimValue(System.Security.Claims.ClaimTypes.MobilePhone)
                : result;
        }

        /// <summary>
        /// 获取当前应用程序标识
        /// </summary>
        /// <param name="session">会话</param>
        public static Guid GetApplicationId(this ISession session) => session.FindClaimValue(ClaimTypes.ApplicationId).ToGuid();

        /// <summary>
        /// 获取当前应用程序标识
        /// </summary>
        /// <param name="session">会话</param>
        public static T GetApplicationId<T>(this ISession session) => Conv.To<T>(session.FindClaimValue(ClaimTypes.ApplicationId));

        /// <summary>
        /// 获取当前应用程序编码
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetApplicationCode(this ISession session) => session.FindClaimValue(ClaimTypes.ApplicationCode);

        /// <summary>
        /// 获取当前应用程序名称
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetApplicationName(this ISession session) => session.FindClaimValue(ClaimTypes.ApplicationName);

        /// <summary>
        /// 获取当前租户标识
        /// </summary>
        /// <param name="session">会话</param>
        public static Guid GetTenantId(this ISession session) => session.FindClaimValue(ClaimTypes.TenantId).ToGuid();

        /// <summary>
        /// 获取当前租户标识
        /// </summary>
        /// <param name="session">会话</param>
        public static T GetTenantId<T>(this ISession session) => Conv.To<T>(session.FindClaimValue(ClaimTypes.TenantId));

        /// <summary>
        /// 获取当前租户编码
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetTenantCode(this ISession session) => session.FindClaimValue(ClaimTypes.TenantCode);

        /// <summary>
        /// 获取当前租户名称
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetTenantName(this ISession session) => session.FindClaimValue(ClaimTypes.TenantName);

        /// <summary>
        /// 获取当前操作人角色标识列表
        /// </summary>
        /// <param name="session">会话</param>
        public static List<Guid> GetRoleIds(this ISession session) => GetRoleIds<Guid>(session);

        /// <summary>
        /// 获取当前操作人角色标识列表
        /// </summary>
        /// <param name="session">会话</param>
        public static List<T> GetRoleIds<T>(this ISession session)
        {
            var result = session.FindClaimValue(ClaimTypes.RoleIds);
            return string.IsNullOrWhiteSpace(result)
                ? Conv.ToList<T>(session.FindClaimValue(System.Security.Claims.ClaimTypes.Role))
                : Conv.ToList<T>(result);
        }

        /// <summary>
        /// 获取当前操作人角色名
        /// </summary>
        /// <param name="session">会话</param>
        public static string GetRoleName(this ISession session) => session.FindClaimValue(ClaimTypes.RoleName);
    }
}
