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
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 架构
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 完整表名
    /// </summary>
    public string FullTableName { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 列映射集合
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMappingMetadata> Columns { get; set; }
}
