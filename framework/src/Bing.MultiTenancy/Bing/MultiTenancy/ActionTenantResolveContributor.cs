namespace Bing.MultiTenancy;

/// <summary>
/// 基于Action的租户解析构造器
/// </summary>
public class ActionTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 构造器名称
    /// </summary>
    public const string ContributorName = "Action";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 解析操作
    /// </summary>
    private readonly Action<ITenantResolveContext> _resolveAction;

    /// <summary>
    /// 初始化一个<see cref="ActionTenantResolveContributor"/>类型的实例
    /// </summary>
    /// <param name="resolveAction">解析操作</param>
    public ActionTenantResolveContributor(Action<ITenantResolveContext> resolveAction)
    {
        _resolveAction = resolveAction ?? throw new ArgumentNullException(nameof(resolveAction));
    }

    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        _resolveAction(context);
        return Task.CompletedTask;
    }
}
