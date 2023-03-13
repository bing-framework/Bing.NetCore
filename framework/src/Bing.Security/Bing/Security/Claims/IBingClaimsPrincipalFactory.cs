using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 安全主体工厂
/// </summary>
public interface IBingClaimsPrincipalFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="existsClaimsPrincipal">安全主体</param>
    Task<ClaimsPrincipal> CreateAsync(ClaimsPrincipal existsClaimsPrincipal = null);

}
