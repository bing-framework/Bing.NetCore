namespace Bing.MultiTenancy;

/// <summary>
/// 通过调用方提供的委托解析租户的贡献者。
/// </summary>
public class ActionTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 用于诊断和解析链路记录的贡献者名称。
    /// </summary>
    public const string ContributorName = "Action";

    /// <inheritdoc />
    public override string Name => ContributorName;

    /// <summary>
    /// 保存调用方提供的同步租户解析操作。
    /// </summary>
    private readonly Action<ITenantResolveContext> _resolveAction;

    /// <summary>
    /// 使用指定解析委托初始化 <see cref="ActionTenantResolveContributor"/> 的实例。
    /// </summary>
    /// <param name="resolveAction">用于更新租户解析上下文的同步操作。</param>
    public ActionTenantResolveContributor(Action<ITenantResolveContext> resolveAction)
    {
        _resolveAction = resolveAction ?? throw new ArgumentNullException(nameof(resolveAction));
    }

    /// <inheritdoc />
    /// <remarks>当前实现同步调用构造时提供的委托，然后返回已完成任务。</remarks>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        _resolveAction(context);
        return Task.CompletedTask;
    }
}
