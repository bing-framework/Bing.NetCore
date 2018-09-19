using System;
using System.Collections.Generic;
using Bing.Helpers;
using Bing.Security.Claims;
using Bing.Sessions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Security.Extensions
{
    /// <summary>
    /// 用户会话扩展
    /// </summary>
    public static partial class SessionExtensions
    {
        /// <summary>
        /// 获取当前操作人标识
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static Guid GetUserId(this ISession session)
        {
            return session.UserId.ToGuid();
        }

        /// <summary>
        /// 获取当前操作人标识
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static T GetUserId<T>(this ISession session)
        {
            return Conv.To<T>(session.UserId);
        }

        /// <summary>
        /// 获取当前操作人用户名
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetUserName(this ISession session)
        {
            return session.UserName;
        }

        /// <summary>
        /// 获取当前操作人姓名
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetFullName(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.FullName);
        }

        /// <summary>
        /// 获取当前操作人电子邮件
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetEmail(this ISession session)
        {
            var result = WebIdentity.Identity.GetValue(ClaimTypes.Email);
            return string.IsNullOrWhiteSpace(result)
                ? WebIdentity.Identity.GetValue(System.Security.Claims.ClaimTypes.Email)
                : result;
        }

        /// <summary>
        /// 获取当前操作人手机号
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetMobile(this ISession session)
        {
            var result = WebIdentity.Identity.GetValue(ClaimTypes.Mobile);
            return string.IsNullOrWhiteSpace(result)
                ? WebIdentity.Identity.GetValue(System.Security.Claims.ClaimTypes.MobilePhone)
                : result;
        }

        /// <summary>
        /// 获取当前应用程序标识
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static Guid GetApplicationId(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.ApplicationId).ToGuid();
        }

        /// <summary>
        /// 获取当前应用程序标识
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static T GetApplicationId<T>(this ISession session)
        {
            return Conv.To<T>(WebIdentity.Identity.GetValue(ClaimTypes.ApplicationId));
        }

        /// <summary>
        /// 获取当前应用程序编码
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetApplicationCode(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.ApplicationCode);
        }

        /// <summary>
        /// 获取当前应用程序名称
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetApplicationName(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.ApplicationName);
        }

        /// <summary>
        /// 获取当前租户标识
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static Guid GetTenantId(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.TenantId).ToGuid();
        }

        /// <summary>
        /// 获取当前租户标识
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static T GetTenantId<T>(this ISession session)
        {
            return Conv.To<T>(WebIdentity.Identity.GetValue(ClaimTypes.TenantId));
        }

        /// <summary>
        /// 获取当前租户编码
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetTenantCode(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.TenantCode);
        }

        /// <summary>
        /// 获取当前租户名称
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetTenantName(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.TenantName);
        }

        /// <summary>
        /// 获取当前操作人角色标识列表
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static List<Guid> GetRoleIds(this ISession session)
        {
            return GetRoleIds<Guid>(session);
        }

        /// <summary>
        /// 获取当前操作人角色标识列表
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static List<T> GetRoleIds<T>(this ISession session)
        {
            return Conv.ToList<T>(WebIdentity.Identity.GetValue(ClaimTypes.RoleIds));
        }

        /// <summary>
        /// 获取当前操作人角色名
        /// </summary>
        /// <param name="session">用户会话</param>
        /// <returns></returns>
        public static string GetRoleName(this ISession session)
        {
            return WebIdentity.Identity.GetValue(ClaimTypes.RoleName);
        }
       
    }
}
