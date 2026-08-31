namespace Bing.MultiTenancy;

/// <summary>
/// 配置应用程序的多租户运行行为和默认解析规则。
/// </summary>
public class MultiTenancyOptions
{
    /// <summary>
    /// 初始化 <see cref="MultiTenancyOptions"/> 的实例，并使用默认租户解析键。
    /// </summary>
    public MultiTenancyOptions()
    {
        TenantKey = TenantResolverConst.DefaultTenantKey;
    }

    /// <summary>
    /// 获取可复用的默认空选项实例。
    /// </summary>
    /// <remarks>
    /// 该实例公开且可变，不应作为需要隔离修改的配置副本。
    /// </remarks>
    public static readonly MultiTenancyOptions Null = new();

    /// <summary>
    /// 获取或设置是否启用多租户架构。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置是否允许不同租户通过连接字符串覆盖使用不同数据库。
    /// </summary>
    public bool IsAllowMultipleDatabase { get; set; }

    /// <summary>
    /// 获取或设置无法从解析管道获取租户时使用的默认租户标识。
    /// </summary>
    public string DefaultTenantId { get; set; } = default!;

    /// <summary>
    /// 获取或设置供租户解析贡献者读取租户标识的键名；默认值为 <see cref="TenantResolverConst.DefaultTenantKey"/>。
    /// </summary>
    public string TenantKey { get; set; }
}
