namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析选项
/// </summary>
public class BingTenantResolveOptions
{
    /// <summary>
    /// 初始化一个<see cref="BingTenantResolveOptions"/>类型的实例
    /// </summary>
    public BingTenantResolveOptions() => TenantResolvers = new List<ITenantResolveContributor>();

    /// <summary>
    /// 租户解析器列表
    /// </summary>
    public List<ITenantResolveContributor> TenantResolvers { get; set; }
}
