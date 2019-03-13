using System.Security.Claims;
using System.Security.Principal;
using Bing.Utils.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 标识(<see cref="IIdentity"/>) 扩展
    /// </summary>
    public static class IdentityExtensions
    {
        /// <summary>
        /// 获取指定键名的值
        /// </summary>
        /// <param name="identity">标识</param>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public static string GetValue(this IIdentity identity, string key)
        {
            var claim = ((ClaimsIdentity) identity).FindFirst(key);
            return (claim != null) ? claim.Value : string.Empty;
        }

        /// <summary>
        /// 获取指定键名的值
        /// </summary>
        /// <typeparam name="T">泛型</typeparam>
        /// <param name="identity">标识</param>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public static T GetValue<T>(this IIdentity identity, string key)
        {
            return Conv.To<T>(identity.GetValue(key));
        }
    }
}
