using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文
/// </summary>
public sealed class DatabaseContext
{
    /// <summary>
    /// 数据库标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库角色
    /// </summary>
    public DatabaseRole Role { get; set; } = DatabaseRole.Default;

    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 映射版本
    /// </summary>
    public string MappingVersion { get; set; }
}
