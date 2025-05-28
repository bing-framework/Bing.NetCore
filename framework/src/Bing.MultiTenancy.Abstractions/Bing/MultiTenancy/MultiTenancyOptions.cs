namespace Bing.MultiTenancy;

/// <summary>
/// 多租户配置
/// </summary>
public class MultiTenancyOptions
{
    /// <summary>
    /// 初始化一个<see cref="MultiTenancyOptions"/>类型的实例
    /// </summary>
    public MultiTenancyOptions()
    {
        TenantKey = TenantResolverConst.DefaultTenantKey;
    }

    /// <summary>
    /// 多租户配置空实例
    /// </summary>
    public static readonly MultiTenancyOptions Null = new();

    /// <summary>
    /// 是否启用多租户架构
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否允许使用多数据库
    /// </summary>
    public bool IsAllowMultipleDatabase { get; set; }

    /// <summary>
    /// 默认租户标识
    /// </summary>
    public string DefaultTenantId { get; set; } = default!;

    /// <summary>
    /// 租户键名。默认值：<see cref="TenantResolverConst.DefaultTenantKey"/>
    /// </summary>
    public string TenantKey { get; set; }
}
