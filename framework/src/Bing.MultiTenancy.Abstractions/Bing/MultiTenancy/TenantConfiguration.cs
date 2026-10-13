using Bing.Data;
using Bing.Helpers;

namespace Bing.MultiTenancy;

/// <summary>
/// 表示包含标识、名称、连接字符串覆盖和启用状态的租户配置。
/// </summary>
[Serializable]
public class TenantConfiguration
{
    /// <summary>
    /// 初始化一个 <see cref="TenantConfiguration"/> 类型的实例。
    /// </summary>
    public TenantConfiguration()
    {
        IsActive = true;
    }

    /// <summary>
    /// 初始化一个 <see cref="TenantConfiguration"/> 类型的实例。
    /// </summary>
    /// <param name="id">租户标识。</param>
    /// <param name="name">租户名称，不能为空。</param>
    public TenantConfiguration(string id, string name)
        : this()
    {
        Check.NotNull(name, nameof(name));
        Id = id;
        Name = name;
        ConnectionStrings = new ConnectionStringCollection();
    }

    /// <summary>
    /// 初始化一个 <see cref="TenantConfiguration"/> 类型的实例。
    /// </summary>
    /// <param name="id">租户标识。</param>
    /// <param name="name">租户名称，不能为空。</param>
    /// <param name="normalizedName">用于不区分大小写比较的规范化租户名称，不能为空。</param>
    public TenantConfiguration(string id, string name, string normalizedName)
        : this(id, name)
    {
        Check.NotNull(normalizedName, nameof(normalizedName));
        NormalizedName = normalizedName;
    }

    /// <summary>
    /// 获取或设置租户的唯一标识。
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// 获取或设置租户的显示名称。
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 获取或设置用于比较和查询的规范化租户名称。
    /// </summary>
    public string NormalizedName { get; set; } = default!;

    /// <summary>
    /// 获取或设置该租户按连接名称索引的连接字符串覆盖。
    /// </summary>
    /// <remarks>
    /// 未配置覆盖时为 <c>null</c>。
    /// </remarks>
    public ConnectionStringCollection? ConnectionStrings { get; set; }

    /// <summary>
    /// 获取或设置租户是否可作为已解析配置使用。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>true</c>。
    /// </remarks>
    public bool IsActive { get; set; }
}
