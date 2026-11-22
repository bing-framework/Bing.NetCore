using System.Linq.Expressions;
using System.Diagnostics.CodeAnalysis;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 按实体映射解析器分区保存的 Mutation Plan 与属性 Getter 缓存。
/// </summary>
internal sealed class SqlMutationPlanCache
{
    private readonly IEntityModelMetadataProvider _models;
    private readonly bool _generatedOnly;
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
    /// <param name="models">生成式模型提供程序。</param>
    /// <param name="generatedOnly">是否禁止反射访问器。</param>
    internal SqlMutationPlanCache(int? planCacheCapacity = null, int? getterCacheCapacity = null,
        IEntityModelMetadataProvider models = null, bool generatedOnly = false)
    {
        _models = models;
        _generatedOnly = generatedOnly;
        ValidateCapacity(planCacheCapacity, nameof(SqlMetadataOptions.MutationPlanCacheCapacity));
        ValidateCapacity(getterCacheCapacity, nameof(SqlMetadataOptions.MutationGetterCacheCapacity));
        _plans = new BoundedLazyCache<SqlMutationPlanCacheKey, SqlMutationPlan>(planCacheCapacity);
        _getters = new BoundedLazyCache<SqlMutationGetterCacheKey, Func<object, object>>(getterCacheCapacity);
    }

    /// <summary>
    /// 获取缓存计划数量。
    /// </summary>
    internal int PlanCount => _plans.Count;

    /// <summary>
    /// 获取缓存 Getter 数量。
    /// </summary>
    internal int GetterCount => _getters.Count;

    /// <summary>
    /// 获取Plan 缓存命中次数。
    /// </summary>
    internal long PlanCacheHitCount => _plans.HitCount;

    /// <summary>
    /// 获取Plan 缓存未命中次数。
    /// </summary>
    internal long PlanCacheMissCount => _plans.MissCount;

    /// <summary>
    /// 获取Plan 缓存旁路次数。
    /// </summary>
    internal long PlanCacheBypassCount => _plans.BypassCount;

    /// <summary>
    /// 获取Plan 缓存淘汰次数。
    /// </summary>
    internal long PlanCacheEvictionCount => _plans.EvictionCount;

    /// <summary>
    /// 获取Getter 缓存命中次数。
    /// </summary>
    internal long GetterCacheHitCount => _getters.HitCount;

    /// <summary>
    /// 获取Getter 缓存未命中次数。
    /// </summary>
    internal long GetterCacheMissCount => _getters.MissCount;

    /// <summary>
    /// 获取Getter 缓存旁路次数。
    /// </summary>
    internal long GetterCacheBypassCount => _getters.BypassCount;

    /// <summary>
    /// 获取 Getter 缓存驱逐次数。
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
    /// 获取或创建 Mutation Plan。
    /// </summary>
    /// <remarks>
    /// 此重载供内部测试验证并发初始化与异常恢复，不应由生产调用方绕过标准映射计划创建。
    /// </remarks>
    /// <param name="key">Mutation Plan 缓存键。</param>
    /// <param name="factory">创建 Mutation Plan 的工厂。</param>
    /// <returns>缓存中的 Mutation Plan 或新创建的计划。</returns>
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
    /// 获取或创建 Mutation Plan。
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
        var propertyName = column.PropertyName?.Trim();
        var key = new SqlMutationGetterCacheKey(sourceType.TypeHandle,
            propertyName?.ToUpperInvariant() ?? string.Empty);
        var lazy = _getters.GetOrAdd(key, () => new Lazy<Func<object, object>>(
            () => CreateGetter(sourceType, propertyName), LazyThreadSafetyMode.ExecutionAndPublication));
        Func<object, object> getter;
        try
        {
            getter = lazy.Value;
        }
        catch
        {
            _getters.RemoveIfCurrent(key, lazy);
            throw;
        }
        return getter(source);
    }

    /// <summary>
    /// 创建公开实例属性 Getter。
    /// </summary>
    /// <param name="sourceType">源对象运行时类型。</param>
    /// <param name="propertyName">要读取的公共实例属性名称。</param>
    /// <returns>将源对象转换为属性值的已编译委托。</returns>
    private Func<object, object> CreateGetter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces |
            DynamicallyAccessedMemberTypes.PublicProperties)] Type sourceType,
        string propertyName)
    {
        var member = FindGeneratedProperty(sourceType, propertyName);
        if (member?.GeneratedGetter != null)
            return member.GeneratedGetter;
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled || _generatedOnly)
            throw new NotSupportedException($"类型 {sourceType.FullName} 的属性 {propertyName} 没有生成的读取器。并发原始值类型也需要显式注册。");
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
    /// 查找当前运行时类型或其声明基类、接口的生成属性元数据。
    /// </summary>
    /// <param name="sourceType">源对象运行时类型。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>匹配的生成属性元数据；不存在时返回 <see langword="null"/>。</returns>
    private EntityPropertyMetadata FindGeneratedProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type sourceType,
        string propertyName)
    {
        if (_models == null)
            return null;
        for (var type = sourceType; type != null; type = type.BaseType)
        {
            var model = _models.GetMetadata(type);
            if (model != null && model.Properties.TryGetValue(propertyName, out var member) &&
                member.GeneratedGetter != null)
                return member;
        }
        foreach (var interfaceType in sourceType.GetInterfaces())
        {
            var model = _models.GetMetadata(interfaceType);
            if (model != null && model.Properties.TryGetValue(propertyName, out var member) &&
                member.GeneratedGetter != null)
                return member;
        }
        return null;
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
