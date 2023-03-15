namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析器
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// 解析当前租户
    /// </summary>
    /// <returns>
    /// 租户ID，租户名称，null（如果无法解析则返回空）
    /// </returns>
    Task<TenantResolveResult> ResolveTenantIdOrNameAsync();
}
