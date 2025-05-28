namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析构造器
/// </summary>
public interface ITenantResolveContributor
{
    /// <summary>
    /// 名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 解析
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    Task ResolveAsync(ITenantResolveContext context);
}
