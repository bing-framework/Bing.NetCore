using System;
using System.Security.Claims;
using Hangfire.Dashboard;

namespace Bing.Admin.Modules;

/// <summary>
/// 校验 Hangfire Dashboard 的访问身份和角色。
/// </summary>
/// <remarks>
/// 未配置角色或当前请求未认证时默认拒绝访问，避免 Dashboard 因宿主未配置认证而公开暴露。
/// </remarks>
internal sealed class AdminHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// 允许访问 Dashboard 的角色编码。
    /// </summary>
    private readonly string _requiredRole;

    /// <summary>
    /// 初始化一个 <see cref="AdminHangfireDashboardAuthorizationFilter" /> 类型的实例。
    /// </summary>
    /// <param name="requiredRole">允许访问 Dashboard 的角色编码。</param>
    public AdminHangfireDashboardAuthorizationFilter(string requiredRole)
    {
        _requiredRole = requiredRole?.Trim();
    }

    /// <inheritdoc />
    public bool Authorize(DashboardContext context)
    {
        if (context == null)
            return false;

        var httpContext = context.GetHttpContext();
        return IsAuthorized(httpContext?.User);
    }

    /// <summary>
    /// 判断用户是否具备 Dashboard 访问权限。
    /// </summary>
    /// <param name="principal">当前请求用户。</param>
    /// <returns>用户已认证且具备配置角色时返回 <see langword="true"/>。</returns>
    internal bool IsAuthorized(ClaimsPrincipal principal)
        => AdminRoleAuthorization.HasRole(principal, _requiredRole);
}
