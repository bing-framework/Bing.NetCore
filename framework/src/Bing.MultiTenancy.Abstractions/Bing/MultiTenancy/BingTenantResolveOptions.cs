namespace Bing.MultiTenancy;

/// <summary>
/// 配置租户解析管道中的贡献者。
/// </summary>
public class BingTenantResolveOptions
{
    /// <summary>
    /// 初始化一个 <see cref="BingTenantResolveOptions"/> 类型的实例。
    /// </summary>
    public BingTenantResolveOptions() => TenantResolvers = new List<ITenantResolveContributor>();

    /// <summary>
    /// 获取或设置租户解析贡献者列表。
    /// </summary>
    /// <remarks>
    /// 集合顺序即贡献者的执行顺序。
    /// </remarks>
    public List<ITenantResolveContributor> TenantResolvers { get; set; }
}
