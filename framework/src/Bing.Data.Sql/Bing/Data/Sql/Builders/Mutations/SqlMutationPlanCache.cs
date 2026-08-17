using System.Linq.Expressions;
using Bing.Data.Sql.Configs;
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
    private readonly BoundedLazyCache<SqlMutationPlanCacheKey, SqlMutationPlan> _plans;

    /// <summary>
    /// 运行时属性 Getter 缓存。
    /// </summary>
    private readonly BoundedLazyCache<SqlMutationGetterCacheKey, Func<object, object>> _getters;

    /// <summary>
    /// 初始化一个 <see cref="SqlMutationPlanCache"/> 类型的实例。
    /// </summary>
    /// <param name="planCacheCapacity">Mutation Plan 缓存容量。</param>
    /// <param name="getterCacheCapacity">属性 Getter 缓存容量。</param>
    internal SqlMutationPlanCache(int? planCacheCapacity = null, int? getterCacheCapacity = null)
    {
        ValidateCapacity(planCacheCapacity, nameof(SqlMetadataOptions.MutationPlanCacheCapacity));
        ValidateCapacity(getterCacheCapacity, nameof(SqlMetadataOptions.MutationGetterCacheCapacity));
        _plans = new BoundedLazyCache<SqlMutationPlanCacheKey, SqlMutationPlan>(planCacheCapacity);
        _getters = new BoundedLazyCache<SqlMutationGetterCacheKey, Func<object, object>>(getterCacheCapacity);
    }

    /// <summary>
    /// 缓存计划数量。
    /// </summary>
    internal int PlanCount => _plans.Count;

    /// <summary>
    /// 缓存 Getter 数量。
    /// </summary>
    internal int GetterCount => _getters.Count;

    /// <summary>
    /// Plan 缓存命中次数。
    /// </summary>
    internal long PlanCacheHitCount => _plans.HitCount;

    /// <summary>
    /// Plan 缓存未命中次数。
    /// </summary>
    internal long PlanCacheMissCount => _plans.MissCount;

    /// <summary>
    /// Plan 缓存旁路次数。
    /// </summary>
    internal long PlanCacheBypassCount => _plans.BypassCount;

    /// <summary>
    /// Plan 缓存淘汰次数。
    /// </summary>
    internal long PlanCacheEvictionCount => _plans.EvictionCount;

    /// <summary>
    /// Getter 缓存命中次数。
    /// </summary>
    internal long GetterCacheHitCount => _getters.HitCount;

    /// <summary>
    /// Getter 缓存未命中次数。
    /// </summary>
    internal long GetterCacheMissCount => _getters.MissCount;

    /// <summary>
    /// Getter 缓存旁路次数。
    /// </summary>
    internal long GetterCacheBypassCount => _getters.BypassCount;

    /// <summary>
    /// Getter 缓存淘汰次数。
    /// </summary>
    internal long GetterCacheEvictionCount => _getters.EvictionCount;

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
        var lazy = _plans.GetOrAdd(key,
            () => new Lazy<SqlMutationPlan>(factory, LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value;
        }
        catch
        {
            _plans.RemoveIfCurrent(key, lazy);
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
        var lazy = _getters.GetOrAdd(key, () => new Lazy<Func<object, object>>(
            () => CreateGetter(sourceType, column.PropertyName), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value(source);
        }
        catch
        {
            _getters.RemoveIfCurrent(key, lazy);
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
    /// 验证缓存容量配置。
    /// </summary>
    /// <param name="capacity">待验证容量。</param>
    /// <param name="parameterName">对应公开配置属性名称。</param>
    private static void ValidateCapacity(int? capacity, string parameterName)
    {
        if (capacity.HasValue && capacity.Value < 0)
            throw new ArgumentOutOfRangeException(parameterName, capacity, "Mutation 缓存容量不能小于 0。");
    }
}