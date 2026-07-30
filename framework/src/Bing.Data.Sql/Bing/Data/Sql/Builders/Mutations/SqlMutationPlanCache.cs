using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 按实体映射解析器分区保存的 Mutation Plan 与属性 Getter 缓存。
/// </summary>
internal sealed class SqlMutationPlanCache
{
    /// <summary>
    /// Mutation Plan 缓存。
    /// </summary>
    private readonly ConcurrentDictionary<SqlMutationPlanCacheKey, Lazy<SqlMutationPlan>> _plans = new();

    /// <summary>
    /// 运行时属性 Getter 缓存。
    /// </summary>
    private readonly ConcurrentDictionary<SqlMutationGetterCacheKey, Lazy<Func<object, object>>> _getters = new();

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
        return GetOrAdd(key, () => SqlMutationPlan.Create(mapping, operation, includes, excludes));
    }

    /// <summary>
    /// 使用调用方提供的工厂获取或创建 Mutation Plan。
    /// </summary>
    /// <remarks>
    /// 此重载供内部测试验证并发初始化与异常恢复，不应由生产调用方绕过标准映射计划创建。
    /// </remarks>
    internal SqlMutationPlan GetOrAdd(SqlMutationPlanCacheKey key, Func<SqlMutationPlan> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        var lazy = _plans.GetOrAdd(key, _ => new Lazy<SqlMutationPlan>(factory, LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value;
        }
        catch
        {
            RemoveIfCurrent(_plans, key, lazy);
            throw;
        }
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
        var lazy = _getters.GetOrAdd(key, _ => new Lazy<Func<object, object>>(
            () => CreateGetter(sourceType, column.PropertyName), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value(source);
        }
        catch
        {
            RemoveIfCurrent(_getters, key, lazy);
            throw;
        }
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

    /// <summary>
    /// 仅在字典仍保存当前惰性实例时移除失败缓存项，避免误删并发调用已恢复的新值。
    /// </summary>
    private static void RemoveIfCurrent<TKey, TValue>(ConcurrentDictionary<TKey, Lazy<TValue>> cache, TKey key,
        Lazy<TValue> current)
    {
        if (cache.TryGetValue(key, out var cached) && ReferenceEquals(cached, current))
            ((ICollection<KeyValuePair<TKey, Lazy<TValue>>>)cache).Remove(new KeyValuePair<TKey, Lazy<TValue>>(key, current));
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