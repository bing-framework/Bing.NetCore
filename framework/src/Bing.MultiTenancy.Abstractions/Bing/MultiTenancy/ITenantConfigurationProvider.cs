namespace Bing.MultiTenancy;

/// <summary>
/// 租户配置提供程序
/// </summary>
public interface ITenantConfigurationProvider
{
    /// <summary>
    /// 获取租户配置
    /// </summary>
    /// <param name="saveResolveResult">是否保存解析结果。默认：false</param>
    Task<TenantConfiguration> GetAsync(bool saveResolveResult = false);
}
