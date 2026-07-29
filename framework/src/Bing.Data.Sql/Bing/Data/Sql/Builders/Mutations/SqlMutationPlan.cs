using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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

/// <summary>
/// 按实体映射解析器分区保存的 Mutation Plan 与属性 Getter 缓存。
/// </summary>
internal sealed class SqlMutationPlanCache
{
    /// <summary>
    /// Mutation Plan 缓存。
    /// </summary>
    private readonly ConcurrentDictionary<SqlMutationPlanCacheKey, SqlMutationPlan> _plans = new();

    /// <summary>
    /// 运行时属性 Getter 缓存。
    /// </summary>
    private readonly ConcurrentDictionary<SqlMutationGetterCacheKey, Func<object, object>> _getters = new();

    /// <summary>
    /// 缓存计划数量。
    /// </summary>
    internal int PlanCount => _plans.Count;

    /// <summary>
    /// 缓存 Getter 数量。
    /// </summary>
    internal int GetterCount => _getters.Count;

    /// <summary>
    /// 获取或创建 Mutation Plan。
    /// </summary>
    public SqlMutationPlan GetOrAdd(EntityMappingMetadata mapping, string providerKey, SqlMutationOperation operation,
        IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        var key = SqlMutationPlanCacheKey.Create(mapping, providerKey, operation, includes, excludes);
        return _plans.GetOrAdd(key, _ => SqlMutationPlan.Create(mapping, operation, includes, excludes));
    }

    /// <summary>
    /// 获取实体或原始值对象的映射属性值。
    /// </summary>
    public object GetValue(object source, ColumnMappingMetadata column)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        var sourceType = source.GetType();
        var key = new SqlMutationGetterCacheKey(sourceType.TypeHandle,
            column.PropertyName?.Trim().ToUpperInvariant() ?? string.Empty);
        var getter = _getters.GetOrAdd(key, _ => CreateGetter(sourceType, column.PropertyName));
        return getter(source);
    }

    /// <summary>
    /// 创建公开实例属性 Getter。
    /// </summary>
    private static Func<object, object> CreateGetter(Type sourceType, string propertyName)
    {
        var property = sourceType.GetProperty(propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);
        if (property == null || property.GetIndexParameters().Length != 0)
            throw new InvalidOperationException($"原始值对象未包含属性 {propertyName}。");
        var source = Expression.Parameter(typeof(object), "source");
        var access = Expression.Property(Expression.Convert(source, sourceType), property);
        return Expression.Lambda<Func<object, object>>(Expression.Convert(access, typeof(object)), source).Compile();
    }
}

/// <summary>
/// Mutation Plan 缓存键。
/// </summary>
internal readonly record struct SqlMutationPlanCacheKey(RuntimeTypeHandle EntityTypeHandle, string ProviderKey,
    string Database, string Schema, string Table, string MappingProfile, string TableRouteKey,
    SqlMutationOperation Operation, string IncludeSignature, string ExcludeSignature)
{
    /// <summary>
    /// 根据映射和列筛选配置创建缓存键。
    /// </summary>
    public static SqlMutationPlanCacheKey Create(EntityMappingMetadata mapping, string providerKey,
        SqlMutationOperation operation, IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        var table = mapping.Table;
        return new SqlMutationPlanCacheKey(mapping.EntityType.TypeHandle, Normalize(providerKey), Normalize(table?.Database),
            Normalize(table?.Schema), Normalize(table?.TableName), Normalize(mapping.MappingProfile),
            Normalize(mapping.TableRouteKey), operation, CreateSignature(includes), CreateSignature(excludes));
    }

    /// <summary>
    /// 规范化缓存文本。
    /// </summary>
    private static string Normalize(string value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// 规范化列筛选签名，忽略调用方的输入大小写和顺序。
    /// </summary>
    private static string CreateSignature(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return string.Empty;
        return string.Join("\u001F", properties.Where(property => string.IsNullOrWhiteSpace(property) == false)
            .Select(Normalize).Distinct(StringComparer.Ordinal).OrderBy(property => property, StringComparer.Ordinal));
    }
}

/// <summary>
/// 属性 Getter 缓存键。
/// </summary>
internal readonly record struct SqlMutationGetterCacheKey(RuntimeTypeHandle SourceTypeHandle, string PropertyName);

/// <summary>
/// 按映射解析器弱引用分区保存 Mutation 缓存，避免跨元数据配置共享计划。
/// </summary>
internal static class SqlMutationPlanCaches
{
    /// <summary>
    /// 各映射解析器的独立缓存。
    /// </summary>
    private static readonly ConditionalWeakTable<IEntityMappingResolver, SqlMutationPlanCache> Caches = new();

    /// <summary>
    /// 获取映射解析器对应的缓存。
    /// </summary>
    public static SqlMutationPlanCache Get(IEntityMappingResolver mappingResolver)
    {
        if (mappingResolver == null)
            throw new ArgumentNullException(nameof(mappingResolver));
        return Caches.GetValue(mappingResolver, _ => new SqlMutationPlanCache());
    }
}