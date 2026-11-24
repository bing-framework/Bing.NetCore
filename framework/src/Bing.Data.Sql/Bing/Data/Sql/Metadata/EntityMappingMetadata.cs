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
    /// 获取或初始化解析映射时使用的完整实体模型元数据。
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

    /// <summary>
    /// 标识默认解析器创建的属性名索引，避免对自定义映射键作出假设。
    /// </summary>
    internal bool HasPropertyNameIndex { get; init; }

    /// <summary>
    /// 按区分大小写的 CLR 属性名查找列映射；自定义映射保留首个匹配项。
    /// </summary>
    /// <param name="propertyName">CLR 属性名。</param>
    /// <returns>匹配的列映射；不存在时返回 <see langword="null"/>。</returns>
    public ColumnMappingMetadata FindColumn(string propertyName)
    {
        if (Columns == null || propertyName == null)
            return null;
        if (HasPropertyNameIndex)
            return Columns.TryGetValue(propertyName, out var column) &&
                   string.Equals(column.PropertyName, propertyName, StringComparison.Ordinal)
                ? column
                : null;
        foreach (var column in Columns.Values)
            if (string.Equals(column.PropertyName, propertyName, StringComparison.Ordinal))
                return column;
        return null;
    }
}
