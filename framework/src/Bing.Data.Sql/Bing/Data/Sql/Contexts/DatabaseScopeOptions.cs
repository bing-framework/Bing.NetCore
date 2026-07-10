using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域选项
/// </summary>
public sealed class DatabaseScopeOptions
{
    /// <summary>
    /// 数据库标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 读取偏好
    /// </summary>
    public SqlReadPreference ReadPreference { get; set; } = SqlReadPreference.Default;

    /// <summary>
    /// 映射配置标识
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 数据库类型。仅用于兼容旧 API。
    /// </summary>
    public DatabaseType? DatabaseType { get; set; }

    /// <summary>
    /// 数据库角色。仅用于兼容旧 API。
    /// </summary>
    public DatabaseRole Role { get; set; } = DatabaseRole.Default;

    /// <summary>
    /// 是否只读。仅用于兼容旧 API。
    /// </summary>
    public bool? ReadOnly { get; set; }
}