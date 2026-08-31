using Bing.Data.Enums;

namespace Bing.Data.Sql.Configs;

/// <summary>
/// 配置实体类型在特定数据源、Provider 和映射配置下的物理表映射。
/// </summary>
public sealed class EntityMappingOptions
{
    /// <summary>
    /// 获取或设置要映射的实体类型。
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 获取或设置映射适用的数据源标识。
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
    /// 获取或设置用于区分运行时表路由映射的键。
    /// </summary>
    /// <remarks>
    /// 该键参与映射候选匹配和缓存键；其职责独立于数据源标识和映射配置名称。
    /// </remarks>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 获取按属性名索引的列映射配置集合；键不区分大小写。
    /// </summary>
    public IDictionary<string, ColumnMappingOptions> Columns { get; } =
        new Dictionary<string, ColumnMappingOptions>(StringComparer.OrdinalIgnoreCase);
}