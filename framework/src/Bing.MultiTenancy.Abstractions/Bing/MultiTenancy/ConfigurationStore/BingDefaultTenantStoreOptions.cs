namespace Bing.MultiTenancy.ConfigurationStore;

/// <summary>
/// 配置默认租户存储加载的预置租户集合。
/// </summary>
public class BingDefaultTenantStoreOptions
{
    /// <summary>
    /// 初始化 <see cref="BingDefaultTenantStoreOptions"/> 的实例，并使用空租户数组。
    /// </summary>
    public BingDefaultTenantStoreOptions() => Tenants = Array.Empty<TenantConfiguration>();

    /// <summary>
    /// 获取或设置供默认租户存储加载的租户配置数组；默认值为空数组。
    /// </summary>
    public TenantConfiguration[] Tenants { get; set; }
}
