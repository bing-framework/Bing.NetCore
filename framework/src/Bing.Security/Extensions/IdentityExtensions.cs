using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Security.Extensions
{
    /// <summary>
    /// 标识(<see cref="IIdentity"/>) 扩展
    /// </summary>
    public static partial class IdentityExtensions
    {
        //#region GetUserId(获取用户标识)

        ///// <summary>
        ///// 获取用户标识
        ///// </summary>
        ///// <param name="identity">标识</param>
        //public static string GetUserId(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return null;
        //    var result = claimsIdentity.GetValue(IdentityModel.JwtClaimTypes.Subject);
        //    return string.IsNullOrWhiteSpace(result)
        //        ? claimsIdentity.GetValue(System.Security.Claims.ClaimTypes.NameIdentifier)
        //        : result;
        //}

        ///// <summary>
        ///// 获取用户标识
        ///// </summary>
        ///// <typeparam name="T">数据类型</typeparam>
        ///// <param name="identity">标识</param>
        //public static T GetUserId<T>(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return default;

        //    var result = claimsIdentity.GetValue(IdentityModel.JwtClaimTypes.Subject);
        //    if (string.IsNullOrWhiteSpace(result))
        //        result = claimsIdentity.GetValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        //    if (string.IsNullOrWhiteSpace(result))
        //        return default;
        //    return Conv.To<T>(result);
        //}

        //#endregion

        //#region GetUserName(获取用户名)

        ///// <summary>
        ///// 获取用户名
        ///// </summary>
        ///// <param name="identity">标识</param>
        //public static string GetUserName(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return null;
        //    var result = claimsIdentity.GetValue(IdentityModel.JwtClaimTypes.Name);
        //    return string.IsNullOrWhiteSpace(result)
        //        ? claimsIdentity.GetValue(System.Security.Claims.ClaimTypes.Name)
        //        : result;
        //}

        //#endregion

        //#region GetEmail(获取电子邮件)

        ///// <summary>
        ///// 获取电子邮件
        ///// </summary>
        ///// <param name="identity">标识</param>
        //public static string GetEmail(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return null;
        //    var result = claimsIdentity.GetValue(IdentityModel.JwtClaimTypes.Email);
        //    return string.IsNullOrWhiteSpace(result)
        //        ? claimsIdentity.GetValue(System.Security.Claims.ClaimTypes.Email)
        //        : result;
        //}

        //#endregion

        //#region GetNickName(获取昵称)

        ///// <summary>
        ///// 获取昵称
        ///// </summary>
        ///// <param name="identity">标识</param>
        //public static string GetNickName(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return null;
        //    var result = claimsIdentity.GetValue(IdentityModel.JwtClaimTypes.GivenName);
        //    return string.IsNullOrWhiteSpace(result)
        //        ? claimsIdentity.GetValue(System.Security.Claims.ClaimTypes.GivenName)
        //        : result;
        //}

        //#endregion

        //#region GetRoles(获取所有角色)

        ///// <summary>
        ///// 获取所有角色
        ///// </summary>
        ///// <param name="identity">标识</param>
        //public static string[] GetRoles(this IIdentity identity)
        //{
        //    Check.NotNull(identity, nameof(identity));
        //    if (!(identity is ClaimsIdentity claimsIdentity))
        //        return new string[0];
        //    var result = GetRoles(claimsIdentity, IdentityModel.JwtClaimTypes.Role);
        //    if (result.Length == 0)
        //        result = GetRoles(claimsIdentity, System.Security.Claims.ClaimTypes.Role);
        //    return result;
        //}

        ///// <summary>
        ///// 获取角色
        ///// </summary>
        ///// <param name="claimsIdentity">声明标识</param>
        ///// <param name="type">类型</param>
        //private static string[] GetRoles(ClaimsIdentity claimsIdentity, string type)
        //{
        //    return claimsIdentity.FindAll(type).SelectMany(m =>
        //    {
        //        var roles = m.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        //        return roles;
        //    }).ToArray();
        //}

        //#endregion
    }
}
