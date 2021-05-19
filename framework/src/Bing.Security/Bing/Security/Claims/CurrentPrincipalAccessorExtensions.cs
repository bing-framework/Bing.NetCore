using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Bing.Security.Claims
{
    /// <summary>
    /// 当前安全主体访问器(<see cref="ICurrentPrincipalAccessor"/>) 扩展
    /// </summary>
    public static class CurrentPrincipalAccessorExtensions
    {
        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="currentPrincipalAccessor">当前安全主体访问器</param>
        /// <param name="claim">声明</param>
        public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, Claim claim) => currentPrincipalAccessor.Change(new[] { claim });

        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="currentPrincipalAccessor">当前安全主体访问器</param>
        /// <param name="claims">声明集合</param>
        public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, IEnumerable<Claim> claims) => currentPrincipalAccessor.Change(new ClaimsIdentity(claims));

        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="currentPrincipalAccessor">当前安全主体访问器</param>
        /// <param name="claimsIdentity">声明身份</param>
        public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, ClaimsIdentity claimsIdentity) => currentPrincipalAccessor.Change(new ClaimsPrincipal(claimsIdentity));
    }
}
