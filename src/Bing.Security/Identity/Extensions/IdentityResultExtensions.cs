using System;
using System.Linq;
using Bing.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Bing.Security.Identity.Extensions
{
    /// <summary>
    /// 授权结果(<see cref="IdentityResult"/>) 扩展
    /// </summary>
    public static class IdentityResultExtensions
    {
        /// <summary>
        /// 失败抛出异常
        /// </summary>
        /// <param name="result">Identity结果</param>
        public static void ThrowIfError(this IdentityResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (result.Succeeded == false)
            {
                throw new Warning(result.Errors.First().Description);
            }
        }
    }
}
