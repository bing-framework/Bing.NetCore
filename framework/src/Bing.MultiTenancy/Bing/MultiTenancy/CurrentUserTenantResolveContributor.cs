using Bing.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 当前用户租户解析构造器
/// </summary>
public class CurrentUserTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "CurrentUser";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
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
