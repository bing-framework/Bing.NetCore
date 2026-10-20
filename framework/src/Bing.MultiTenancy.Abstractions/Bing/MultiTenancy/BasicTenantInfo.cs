namespace Bing.MultiTenancy;

/// <summary>
/// 仅承载租户标识和可选名称的轻量租户信息。
/// </summary>
public class BasicTenantInfo
{
    /// <summary>
    /// 获取租户标识。
    /// </summary>
    /// <remarks>
    /// 无当前租户或尚未解析时为 <c>null</c>。
    /// </remarks>
    public string? TenantId { get; }

    /// <summary>
    /// 获取租户的可选显示名称。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 初始化一个 <see cref="BasicTenantInfo"/> 类型的实例。
    /// </summary>
    /// <param name="tenantId">租户标识；可以为 <c>null</c>。</param>
    /// <param name="name">租户的可选显示名称。</param>
    public BasicTenantInfo(string? tenantId, string? name = null)
    {
        TenantId = tenantId;
        Name = name;
    }
}
