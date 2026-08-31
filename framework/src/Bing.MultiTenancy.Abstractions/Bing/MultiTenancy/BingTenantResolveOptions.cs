namespace Bing.MultiTenancy;

/// <summary>
/// 配置租户解析管道中的贡献者。
/// </summary>
public class BingTenantResolveOptions
{
    /// <summary>
    /// 初始化 <see cref="BingTenantResolveOptions"/> 的实例，并创建空的可变贡献者列表。
    /// </summary>
    public BingTenantResolveOptions() => TenantResolvers = new List<ITenantResolveContributor>();

    /// <summary>
    /// 获取或设置租户解析贡献者列表；集合顺序即贡献者的执行顺序。
    /// </summary>
    public List<ITenantResolveContributor> TenantResolvers { get; set; }
}
