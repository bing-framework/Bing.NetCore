namespace Bing.MultiTenancy;

/// <summary>
/// 保存租户解析管道产生的候选租户和参与解析的贡献者。
/// </summary>
public class TenantResolveResult
{
    /// <summary>
    /// 获取或设置解析出的候选租户标识或名称。
    /// </summary>
    /// <remarks>
    /// 尚未解析时为 <c>null</c>。
    /// </remarks>
    public string? TenantIdOrName { get; set; }

    /// <summary>
    /// 获取实际参与本次解析的贡献者名称列表。
    /// </summary>
    /// <remarks>
    /// 用于追踪解析链路。
    /// </remarks>
    public List<string> AppliedResolvers { get; }

    /// <summary>
    /// 初始化一个 <see cref="TenantResolveResult"/> 类型的实例。
    /// </summary>
    public TenantResolveResult() => AppliedResolvers = new List<string>();
}
