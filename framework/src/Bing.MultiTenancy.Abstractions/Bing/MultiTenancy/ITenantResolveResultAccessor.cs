namespace Bing.MultiTenancy;

/// <summary>
/// 租户解析结果访问器
/// </summary>
public interface ITenantResolveResultAccessor
{
    /// <summary>
    /// 获取或设置租户解析结果
    /// </summary>
    TenantResolveResult? Result { get; set; }
}
