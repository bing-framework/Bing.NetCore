namespace Bing.MultiTenancy;

/// <summary>
/// 租户存储器
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// 通过名称查找租户配置
    /// </summary>
    /// <param name="name">名称</param>
    Task<TenantConfiguration> FindByNameAsync(string name);

    /// <summary>
    /// 通过租户ID查找租户配置
    /// </summary>
    /// <param name="id">租户ID</param>
    Task<TenantConfiguration> FindByIdAsync(string id);
}
