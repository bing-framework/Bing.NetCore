namespace Bing.MultiTenancy;

/// <summary>
/// 从特定来源解析当前租户的贡献者。
/// </summary>
public interface ITenantResolveContributor
{
    /// <summary>
    /// 获取用于诊断和记录已应用贡献者的稳定名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 从贡献者支持的来源解析租户，并更新解析上下文。
    /// </summary>
    /// <param name="context">用于读取服务、写入候选租户或终止解析链的上下文。</param>
    Task ResolveAsync(ITenantResolveContext context);
}
