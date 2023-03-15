namespace Bing.MultiTenancy;

/// <summary>
/// 基本租户信息
/// </summary>
public class BasicTenantInfo
{
    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 租户编码
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 初始化一个<see cref="BasicTenantInfo"/>类型的实例
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="code">租户编码</param>
    /// <param name="name">租户名称</param>
    public BasicTenantInfo(string tenantId, string code = null, string name = null)
    {
        TenantId = tenantId;
        Code = code;
        Name = name;
    }
}
