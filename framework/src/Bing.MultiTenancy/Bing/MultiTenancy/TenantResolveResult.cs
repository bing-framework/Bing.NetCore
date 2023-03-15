namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析结果
/// </summary>
public class TenantResolveResult
{
    /// <summary>
    /// 租户ID或租户名称
    /// </summary>
    public string TenantIdOrName { get; set; }

    /// <summary>
    /// 应用解析器列表
    /// </summary>
    public List<string> AppliedResolvers { get; }

    /// <summary>
    /// 初始化一个<see cref="TenantResolveResult"/>类型的实例
    /// </summary>
    public TenantResolveResult() => AppliedResolvers = new List<string>();
}
