namespace Bing.MultiTenancy.ConfigurationStore;

/// <summary>
/// 默认租户存储配置选项
/// </summary>
public class BingDefaultTenantStoreOptions
{
    /// <summary>
    /// 初始化一个<see cref="BingDefaultTenantStoreOptions"/>类型的实例
    /// </summary>
    public BingDefaultTenantStoreOptions() => Tenants = Array.Empty<TenantConfiguration>();

    /// <summary>
    /// 租户配置数组
    /// </summary>
    public TenantConfiguration[] Tenants { get; set; }
}
