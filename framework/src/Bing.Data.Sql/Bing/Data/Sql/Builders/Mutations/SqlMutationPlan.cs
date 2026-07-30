using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 实体 Mutation 操作类型。
/// </summary>
internal enum SqlMutationOperation
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// 可跨实体实例复用的 Mutation 映射计划。
/// </summary>
internal sealed class SqlMutationPlan
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationPlan"/> 类型的实例。
    /// </summary>
    private SqlMutationPlan(EntityMappingMetadata mapping, IReadOnlyList<ColumnMappingMetadata> writeColumns,
        IReadOnlyList<ColumnMappingMetadata> keys, IReadOnlyList<ColumnMappingMetadata> concurrencyColumns)
    {
        Mapping = mapping;
        WriteColumns = writeColumns;
        Keys = keys;
        ConcurrencyColumns = concurrencyColumns;
    }

    /// <summary>
    /// 实体映射。
    /// </summary>
    public EntityMappingMetadata Mapping { get; }

    /// <summary>
    /// 当前操作应写入的列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> WriteColumns { get; }

    /// <summary>
    /// 主键列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> Keys { get; }

    /// <summary>
    /// 并发令牌列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> ConcurrencyColumns { get; }

    /// <summary>
    /// 创建指定操作和列筛选配置的计划。
    /// </summary>
    public static SqlMutationPlan Create(EntityMappingMetadata mapping, SqlMutationOperation operation,
        IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        var includeSet = CreatePropertySet(includes);
        var excludeSet = CreatePropertySet(excludes);
        var writeColumns = operation switch
        {
            SqlMutationOperation.Insert => mapping.Columns.Values.Where(column => column.CanInsert),
            SqlMutationOperation.Update => mapping.Columns.Values.Where(column => column.CanUpdate),
            _ => Enumerable.Empty<ColumnMappingMetadata>()
        };
        writeColumns = writeColumns.Where(column =>
            (includeSet == null || includeSet.Contains(column.PropertyName)) &&
            (excludeSet == null || excludeSet.Contains(column.PropertyName) == false));
        return new SqlMutationPlan(mapping, writeColumns.ToArray(),
            mapping.Columns.Values.Where(column => column.IsKey).ToArray(),
            mapping.Columns.Values.Where(column => column.IsConcurrencyToken).ToArray());
    }

    /// <summary>
    /// 创建忽略大小写的属性筛选集合。
    /// </summary>
    private static HashSet<string> CreatePropertySet(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;
        return new HashSet<string>(properties.Where(property => string.IsNullOrWhiteSpace(property) == false),
            StringComparer.OrdinalIgnoreCase);
    }
}