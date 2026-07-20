using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射元数据
/// </summary>
public sealed class EntityMappingMetadata
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType? DatabaseType { get; set; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 数据库目录
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
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 应用逻辑命名策略后的最终物理表名。
    /// </summary>
    public string ResolvedTableName { get; set; }

    /// <summary>
    /// 完整表名
    /// </summary>
    [Obsolete("FullTableName 不再作为 SQL 生成依据，请使用 TableReference。")]
    public string FullTableName { get; set; }

    /// <summary>
    /// 结构化表标识符
    /// </summary>
    public TableIdentifier Table { get; set; }

    /// <summary>
    /// 最终结构化表引用
    /// </summary>
    public SqlTableReference TableReference { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 列映射集合
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMappingMetadata> Columns { get; set; }
}
