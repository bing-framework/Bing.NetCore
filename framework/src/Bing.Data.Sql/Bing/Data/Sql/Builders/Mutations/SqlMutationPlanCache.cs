using System.Collections.Concurrent;
using System.Linq.Expressions;
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
    /// <param name="mapping">实体表和列映射。</param>
    /// <param name="providerKey">Provider 唯一标识，用于隔离方言相关计划。</param>
    /// <param name="operation">需要生成计划的 Mutation 操作。</param>
    /// <param name="includes">显式包含的属性集合。</param>
    /// <param name="excludes">显式排除的属性集合。</param>
    /// <returns>由映射、Provider 和列筛选维度唯一确定的 Mutation 计划。</returns>
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
    /// <param name="source">实体或并发原始值对象。</param>
    /// <param name="column">待读取的映射列。</param>
    /// <returns>源对象中对应公共实例属性的值。</returns>
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
    /// <param name="sourceType">源对象运行时类型。</param>
    /// <param name="propertyName">要读取的公共实例属性名称。</param>
    /// <returns>将源对象转换为属性值的已编译委托。</returns>
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
    /// <typeparam name="TKey">缓存键类型。</typeparam>
    /// <typeparam name="TValue">缓存值类型。</typeparam>
    /// <param name="cache">包含惰性缓存值的并发字典。</param>
    /// <param name="key">失败项的缓存键。</param>
    /// <param name="current">本次调用观察到的失败惰性实例。</param>
    private static void RemoveIfCurrent<TKey, TValue>(ConcurrentDictionary<TKey, Lazy<TValue>> cache, TKey key,
        Lazy<TValue> current)
    {
        if (cache.TryGetValue(key, out var cached) && ReferenceEquals(cached, current))
            ((ICollection<KeyValuePair<TKey, Lazy<TValue>>>)cache).Remove(new KeyValuePair<TKey, Lazy<TValue>>(key, current));
    }
}