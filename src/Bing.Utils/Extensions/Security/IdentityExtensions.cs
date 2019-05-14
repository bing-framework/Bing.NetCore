using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Bing.Utils.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 标识(<see cref="IIdentity"/>) 扩展
    /// </summary>
    public static partial class IdentityExtensions
    {
        #region GetValue(获取指定类型的Claim值)

        /// <summary>
        /// 获取指定类型的Claim值
        /// </summary>
        /// <param name="identity">标识</param>
        /// <param name="type">类型</param>
        public static string GetValue(this IIdentity identity, string type)
        {
            Check.NotNull(identity, nameof(identity));
            if (!(identity is ClaimsIdentity claimsIdentity))
            {
                return null;
            }

            return claimsIdentity.FindFirst(type)?.Value ?? string.Empty;
        }

        /// <summary>
        /// 获取指定类型的Claim值
        /// </summary>
        /// <typeparam name="T">泛型</typeparam>
        /// <param name="identity">标识</param>
        /// <param name="type">类型</param>
        public static T GetValue<T>(this IIdentity identity, string type)
        {
            var result = identity.GetValue(type);
            return result.IsEmpty() ? default(T) : Conv.To<T>(result);
        }

        #endregion

        #region GetValues(获取指定类型的所有Claim值)

        /// <summary>
        /// 获取指定类型的所有Claim值
        /// </summary>
        /// <param name="identity">标识</param>
        /// <param name="type">类型</param>
        public static string[] GetValues(this IIdentity identity, string type)
        {
            Check.NotNull(identity, nameof(identity));
            if (!(identity is ClaimsIdentity claimsIdentity))
            {
                return null;
            }

            return claimsIdentity.Claims.Where(x => x.Type == type).Select(x => x.Value).ToArray();
        }

        #endregion

        #region RemoveClaim(移除指定类型的声明)

        /// <summary>
        /// 移除指定类型的声明
        /// </summary>
        /// <param name="identity">标识</param>
        /// <param name="claimType">声明类型</param>
        public static void RemoveClaim(this IIdentity identity, string claimType)
        {
            Check.NotNull(identity, nameof(identity));
            if (!(identity is ClaimsIdentity claimsIdentity))
            {
                return;
            }

            var claim = claimsIdentity.FindFirst(claimType);
            if (claim == null)
            {
                return;
            }

            claimsIdentity.RemoveClaim(claim);
        }

        #endregion
    }
}
