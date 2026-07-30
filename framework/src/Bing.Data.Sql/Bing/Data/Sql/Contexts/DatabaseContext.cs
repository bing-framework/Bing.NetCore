namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文
/// </summary>
public sealed class DatabaseContext
{
    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 当前上下文指定的 SQL Provider 唯一标识。
    /// </summary>
    /// <remarks>数据源未指定 Provider Key 时使用该值。</remarks>
    public string ProviderKey { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 读取偏好
    /// </summary>
    public SqlReadPreference ReadPreference { get; set; } = SqlReadPreference.Default;

    /// <summary>
    /// 映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 当前数据源描述
    /// </summary>
    public SqlDataSourceDescriptor DataSource { get; set; }
}
