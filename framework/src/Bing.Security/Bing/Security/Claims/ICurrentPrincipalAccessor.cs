using System.Security.Claims;
using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Security.Claims;

/// <summary>
/// 当前身份主体访问器
/// </summary>
[IgnoreAspect]
public interface ICurrentPrincipalAccessor: ISingletonDependency
{
    /// <summary>
    /// 身份主体
    /// </summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    /// 变更
    /// </summary>
    /// <param name="principal">身份主体</param>
    IDisposable Change(ClaimsPrincipal principal);
}
