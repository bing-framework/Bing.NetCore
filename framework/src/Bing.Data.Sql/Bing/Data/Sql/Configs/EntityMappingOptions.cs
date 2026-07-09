using Bing.Data.Enums;

namespace Bing.Data.Sql.Configs;

/// <summary>
/// 实体映射配置
/// </summary>
public sealed class EntityMappingOptions
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 数据库键
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
    /// 架构
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 映射版本
    /// </summary>
    public string MappingVersion { get; set; }

    /// <summary>
    /// 列映射配置集合
    /// </summary>
    public IDictionary<string, ColumnMappingOptions> Columns { get; } =
        new Dictionary<string, ColumnMappingOptions>(StringComparer.OrdinalIgnoreCase);
}