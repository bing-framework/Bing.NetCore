namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 描述实体到数据库对象的最终映射结果。
/// </summary>
public sealed class EntityMappingMetadata
{
    /// <summary>
    /// 获取或初始化实体类型。
    /// </summary>
    public Type EntityType { get; init; }

    /// <summary>
    /// 解析映射时使用的完整实体模型元数据。
    /// </summary>
    public EntityModelMetadata Model { get; init; }

    /// <summary>
    /// 获取或初始化映射配置名称。
    /// </summary>
    public string MappingProfile { get; init; }

    /// <summary>
    /// 获取或初始化实体对应的表引用。
    /// </summary>
    public SqlTableReference Table { get; init; }

    /// <summary>
    /// 获取或初始化表路由键。
    /// </summary>
    public string TableRouteKey { get; init; }

    /// <summary>
    /// 获取或初始化列映射集合。
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMappingMetadata> Columns { get; init; }
}
