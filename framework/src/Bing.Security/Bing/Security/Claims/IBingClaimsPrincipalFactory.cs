using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 提供用于创建 <see cref="ClaimsPrincipal"/>（身份主体）的工厂接口。
/// </summary>
public interface IBingClaimsPrincipalFactory
{
    /// <summary>
    /// 创建一个新的 <see cref="ClaimsPrincipal"/>（身份主体）。
    /// </summary>
    /// <param name="existsClaimsPrincipal">现有的身份主体（可选）。如果提供，则在此基础上创建新的身份主体。</param>
    /// <returns>
    /// 返回一个包含用户声明（Claims）的 <see cref="ClaimsPrincipal"/> 实例，表示经过身份验证的用户。
    /// </returns>
    /// <remarks>
    /// 该方法通常用于 用户身份验证（Authentication），根据已有的 `ClaimsPrincipal`，创建一个新的身份主体。<br /><br />
    /// 
    /// 应用场景：<br />
    /// - 登录流程：用户成功登录后创建新的身份主体，并存储在 `HttpContext.User` 中。<br />
    /// - Token 认证：基于现有 `ClaimsPrincipal` 创建新的身份信息，用于 JWT 或 OAuth 认证。
    /// </remarks>
    Task<ClaimsPrincipal> CreateAsync(ClaimsPrincipal existsClaimsPrincipal = null);

    /// <summary>
    /// 创建一个动态的 <see cref="ClaimsPrincipal"/>（身份主体）。
    /// </summary>
    /// <param name="existsClaimsPrincipal">现有的身份主体（可选）。如果提供，则在此基础上创建新的动态身份主体。</param>
    /// <returns>
    /// 返回一个包含用户声明（Claims）的动态 <see cref="ClaimsPrincipal"/> 实例，通常用于运行时动态变更用户身份信息的场景。
    /// </returns>
    /// <remarks>
    /// 该方法主要用于 动态身份管理，允许在运行时调整用户的声明信息（Claims）。<br /><br />
    ///
    /// 应用场景：<br />
    /// - 动态权限变更：在用户操作过程中，实时更新用户的角色、权限信息。<br />
    /// - 多租户切换：在 SaaS 应用中，允许用户在不同租户之间动态切换身份。
    /// </remarks>
    Task<ClaimsPrincipal> CreateDynamicAsync(ClaimsPrincipal existsClaimsPrincipal = null);
}
