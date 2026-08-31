namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析构造器基类
/// </summary>
public abstract class TenantResolveContributorBase : ITenantResolveContributor
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract Task ResolveAsync(ITenantResolveContext context);
}
