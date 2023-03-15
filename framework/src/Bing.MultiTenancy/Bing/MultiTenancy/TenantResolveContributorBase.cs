namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析构造器基类
/// </summary>
public abstract class TenantResolveContributorBase : ITenantResolveContributor
{
    /// <summary>
    /// 名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    public abstract Task ResolveAsync(ITenantResolveContext context);
}
