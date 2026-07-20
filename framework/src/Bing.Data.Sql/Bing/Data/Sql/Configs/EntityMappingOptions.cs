using Bing.Data.Sql.Metadata;

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
    /// 实体映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 同连接数据库目录
    /// </summary>
    public string Catalog { get; set; }

    /// <summary>
    /// 物理架构
    /// </summary>
    public string PhysicalSchema { get; set; }

    /// <summary>
    /// 逻辑架构
    /// </summary>
    public string LogicalSchema { get; set; }

    /// <summary>
    /// 架构
    /// </summary>
    [Obsolete("请改用 LogicalSchema 或 PhysicalSchema。旧 Schema 默认按逻辑架构处理。")]
    public string Schema { get; set; }

    /// <summary>
    /// 旧 Schema 兼容方式
    /// </summary>
    public SchemaCompatibilityMode SchemaCompatibilityMode { get; set; } = SchemaCompatibilityMode.Auto;

    /// <summary>
    /// 逻辑架构命名方式
    /// </summary>
    public LogicalTableNamingMode NamingMode { get; set; } = LogicalTableNamingMode.Prefix;

    /// <summary>
    /// Oracle 数据库链接
    /// </summary>
    public string DatabaseLink { get; set; }

    /// <summary>
    /// SQLite 已附加数据库别名
    /// </summary>
    public string AttachedAlias { get; set; }

    /// <summary>
    /// 表名
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