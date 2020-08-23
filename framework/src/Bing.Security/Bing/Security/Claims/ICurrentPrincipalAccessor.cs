using System;
using System.Security.Claims;
using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Security.Claims
{
    /// <summary>
    /// 当前安全主体访问器
    /// </summary>
    [Ignore]
    public interface ICurrentPrincipalAccessor : ISingletonDependency
    {
        /// <summary>
        /// 安全主体
        /// </summary>
        ClaimsPrincipal Principal { get; }

        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="principal">安全主体</param>
        IDisposable Change(ClaimsPrincipal principal);
    }
}
