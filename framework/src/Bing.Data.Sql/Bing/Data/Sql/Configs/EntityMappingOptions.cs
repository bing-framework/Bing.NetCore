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
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型，仅用于选择映射配置。
    /// </summary>
    public DatabaseType? DatabaseType { get; set; }

    /// <summary>
    /// 实体映射配置名称。
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// SQL Server 数据库名称。
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 数据库架构。MySQL 和 Doris 中表示数据库名称。
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 最终物理表名。
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 列映射配置集合
    /// </summary>
    public IDictionary<string, ColumnMappingOptions> Columns { get; } =
        new Dictionary<string, ColumnMappingOptions>(StringComparer.OrdinalIgnoreCase);
}