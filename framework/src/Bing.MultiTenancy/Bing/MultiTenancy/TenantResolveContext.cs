namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析上下文
/// </summary>
public class TenantResolveContext : ITenantResolveContext
{
    /// <summary>
    /// 初始化一个<see cref="TenantResolveContext"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public TenantResolveContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <inheritdoc />
    public string? TenantIdOrName { get; set; }

    /// <inheritdoc />
    public bool Handled { get; set; }

    /// <summary>
    /// 确定当前上下文是否已解析候选租户或已终止解析链。
    /// </summary>
    /// <returns>已存在候选租户或上下文已标记为处理完成时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public bool HasResolvedTenantOrHost() => Handled || TenantIdOrName != null;
}
