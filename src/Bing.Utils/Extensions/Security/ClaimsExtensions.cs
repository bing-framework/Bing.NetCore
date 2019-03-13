using System;
using System.Collections.Generic;
using System.Security.Claims;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 声明(<see cref="Claim"/>) 扩展
    /// </summary>
    public static class ClaimsExtensions
    {
        /// <summary>
        /// 尝试添加声明到列表当中。如果不存在，则添加；存在，不添加也不抛异常
        /// </summary>
        /// <param name="claims">声明列表</param>
        /// <param name="type">类型</param>
        /// <param name="value">值</param>
        /// <param name="valueType">值类型</param>
        public static void TryAddClaim(this List<Claim> claims, string type, string value,
            string valueType = ClaimValueTypes.String)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (claims.Exists(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            claims.Add(new Claim(type, value, valueType));
        }
    }
}
