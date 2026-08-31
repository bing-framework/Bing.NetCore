using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 实体 Mutation 操作类型。
/// </summary>
internal enum SqlMutationOperation
{
    /// <summary>
    /// 基于实体映射生成 Insert 命令。
    /// </summary>
    Insert,

    /// <summary>
    /// 基于实体映射生成 Update 命令。
    /// </summary>
    Update,

    /// <summary>
    /// 基于实体映射生成 Delete 命令。
    /// </summary>
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
    /// <param name="mapping">实体表和列映射。</param>
    /// <param name="writeColumns">当前操作应写入的列。</param>
    /// <param name="keys">主键列。</param>
    /// <param name="concurrencyColumns">并发令牌列。</param>
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
    /// <param name="mapping">实体表和列映射。</param>
    /// <param name="operation">需要生成计划的 Mutation 操作。</param>
    /// <param name="includes">显式包含的实体属性集合。</param>
    /// <param name="excludes">显式排除的实体属性集合。</param>
    /// <returns>包含可写列、主键和并发令牌列的可复用 Mutation 计划。</returns>
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
    /// <param name="properties">调用方提供的属性名称集合。</param>
    /// <returns>去除空白项后的忽略大小写集合；未提供有效属性时返回 <see langword="null"/>。</returns>
    private static HashSet<string> CreatePropertySet(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;
        return new HashSet<string>(properties.Where(property => string.IsNullOrWhiteSpace(property) == false),
            StringComparer.OrdinalIgnoreCase);
    }
}