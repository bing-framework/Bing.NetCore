using System.Security.Claims;
using System.Threading;

namespace Bing.Security.Claims
{
    /// <summary>
    /// 当前线程安全主体访问器
    /// </summary>
    public class ThreadCurrentPrincipalAccessor : CurrentPrincipalAccessorBase
    {
        /// <summary>
        /// 获取安全主体
        /// </summary>
        protected override ClaimsPrincipal GetClaimsPrincipal() => Thread.CurrentPrincipal as ClaimsPrincipal;
    }
}
