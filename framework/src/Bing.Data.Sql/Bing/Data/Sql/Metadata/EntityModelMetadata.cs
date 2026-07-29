using System.Collections.ObjectModel;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体模型元数据。
/// </summary>
public sealed class EntityModelMetadata
{
    /// <summary>
    /// 初始化一个<see cref="EntityModelMetadata"/>类型的实例。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="tableName">原始表名。</param>
    /// <param name="schema">原始架构名。</param>
    /// <param name="properties">属性元数据集合。</param>
    public EntityModelMetadata(Type entityType, string tableName, string schema,
        IEnumerable<EntityPropertyMetadata> properties)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        TableName = tableName;
        Schema = schema;
        var values = (properties ?? Enumerable.Empty<EntityPropertyMetadata>())
            .Where(property => property != null)
            .ToDictionary(property => property.PropertyName, StringComparer.OrdinalIgnoreCase);
        Properties = new ReadOnlyDictionary<string, EntityPropertyMetadata>(values);
    }

    /// <summary>
    /// 实体类型。
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 原始表名。
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// 原始架构名。
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// 按 CLR 属性名索引的属性元数据集合。
    /// </summary>
    public IReadOnlyDictionary<string, EntityPropertyMetadata> Properties { get; }
}