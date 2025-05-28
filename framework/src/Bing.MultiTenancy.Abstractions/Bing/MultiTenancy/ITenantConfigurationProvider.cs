namespace Bing.MultiTenancy;

/// <summary>
/// 租户配置信息提供程序
/// </summary>
public interface ITenantConfigurationProvider
{
    /// <summary>
    /// 获取当前租户的配置信息。
    /// </summary>
    /// <param name="saveResolveResult">是否保存解析结果。默认：false</param>
    /// <returns>
    /// 返回 <see cref="TenantConfiguration"/>，如果未解析到租户，则返回 <c>null</c>。
    /// </returns>
    Task<TenantConfiguration?> GetAsync(bool saveResolveResult = false);
}
