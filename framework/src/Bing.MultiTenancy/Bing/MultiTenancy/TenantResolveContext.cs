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

    /// <summary>
    /// 租户ID或者租户名称
    /// </summary>
    public string? TenantIdOrName { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// 是否已经解析租户或主机
    /// </summary>
    public bool HasResolvedTenantOrHost() => Handled || TenantIdOrName != null;
}
