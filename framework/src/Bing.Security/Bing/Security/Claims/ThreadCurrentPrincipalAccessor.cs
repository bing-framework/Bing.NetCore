using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 当前线程安全主体访问器
/// </summary>
public class ThreadCurrentPrincipalAccessor : CurrentPrincipalAccessorBase
{
    /// <summary>
    /// 获取安全主体
    /// </summary>
    /// <returns>当前线程的安全主体；不是 <see cref="ClaimsPrincipal"/> 时返回 null。</returns>
    protected override ClaimsPrincipal GetClaimsPrincipal() => Thread.CurrentPrincipal as ClaimsPrincipal;
}
