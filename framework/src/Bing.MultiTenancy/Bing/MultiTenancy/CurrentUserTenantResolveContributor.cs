using Bing.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 从当前已认证用户解析租户的贡献者。
/// </summary>
public class CurrentUserTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 用于诊断和解析链路记录的贡献者名称。
    /// </summary>
    public const string ContributorName = "CurrentUser";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <inheritdoc />
    /// <remarks>仅在当前用户已认证时写入其租户标识，并将解析上下文标记为已处理。</remarks>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var currentUser = context.ServiceProvider.GetRequiredService<ICurrentUser>();
        if (currentUser.IsAuthenticated)
        {
            context.Handled = true;
            context.TenantIdOrName = currentUser.TenantId;
        }
        return Task.CompletedTask;
    }
}
