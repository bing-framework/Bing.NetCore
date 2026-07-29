namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射元数据
/// </summary>
public sealed class EntityMappingMetadata
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; init; }

    /// <summary>
    /// 解析映射时使用的完整实体模型元数据。
    /// </summary>
    public EntityModelMetadata Model { get; init; }

    /// <summary>
    /// 映射配置名称
    /// </summary>
    public string MappingProfile { get; init; }

    /// <summary>
    /// 表引用。
    /// </summary>
    public SqlTableReference Table { get; init; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; init; }

    /// <summary>
    /// 列映射集合
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMappingMetadata> Columns { get; init; }
}
