using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 定义用于扩展 <see cref="ClaimsPrincipal"/> 的贡献者接口。
/// </summary>
public interface IBingClaimsPrincipalContributor
{
    /// <summary>
    /// 贡献（扩展）身份声明信息（Claims）。
    /// </summary>
    /// <param name="context">贡献上下文</param>
    Task ContributeAsync(BingClaimsPrincipalContributorContext context);
}
