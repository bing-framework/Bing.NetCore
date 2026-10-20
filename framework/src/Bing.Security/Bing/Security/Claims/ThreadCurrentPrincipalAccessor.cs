using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 当前线程安全主体访问器
/// </summary>
public class ThreadCurrentPrincipalAccessor : CurrentPrincipalAccessorBase
{
    /// <inheritdoc />
    protected override ClaimsPrincipal GetClaimsPrincipal() => Thread.CurrentPrincipal as ClaimsPrincipal;
}
