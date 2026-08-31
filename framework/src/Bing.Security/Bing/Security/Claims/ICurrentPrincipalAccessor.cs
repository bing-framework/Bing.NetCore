using System.Security.Claims;
using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Security.Claims;

/// <summary>
/// 提供当前执行上下文的身份主体，并支持临时切换。
/// </summary>
[IgnoreAspect]
public interface ICurrentPrincipalAccessor: ISingletonDependency
{
    /// <summary>
    /// 获取当前执行上下文关联的身份主体。
    /// </summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    /// 临时切换当前执行上下文的身份主体。
    /// </summary>
    /// <param name="principal">在临时作用域内使用的身份主体。</param>
    /// <returns>释放后恢复切换前身份主体的作用域对象。</returns>
    IDisposable Change(ClaimsPrincipal principal);
}
