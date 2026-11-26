using System.Runtime.CompilerServices;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 按优先级组合实体模型元数据提供器。
/// </summary>
public sealed class CompositeEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <summary>
    /// 已排序的元数据提供器集合。
    /// </summary>
    private readonly IReadOnlyList<IEntityModelMetadataProvider> _providers;

    /// <summary>显式提供器及兼容模式下全局生成提供器的数量。</summary>
    private readonly int _registeredProviderCount;

    // 仅缓存内置反射回退；弱键不延长可卸载实体类型的生命周期。
    private readonly ConditionalWeakTable<Type, EntityModelMetadata> _fallbackMetadata = new();

    /// <summary>
    /// 初始化一个 <see cref="CompositeEntityModelMetadataProvider"/> 类型的实例。
    /// </summary>
    public CompositeEntityModelMetadataProvider()
        : this(null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CompositeEntityModelMetadataProvider"/> 类型的实例。
    /// </summary>
    /// <param name="providers">前置自定义提供器集合。</param>
    public CompositeEntityModelMetadataProvider(IEnumerable<IEntityModelMetadataProvider> providers)
        : this(providers, false)
    {
    }

    /// <summary>
    /// 创建可明确禁止默认反射提供器的模型组合。
    /// </summary>
    /// <param name="providers">由宿主注册的提供器，按登记顺序处理。</param>
    /// <param name="generatedOnly">是否禁止全局注册及默认反射回退。</param>
    public CompositeEntityModelMetadataProvider(IEnumerable<IEntityModelMetadataProvider> providers, bool generatedOnly)
    {
        var values = new List<IEntityModelMetadataProvider>();
        if (providers != null)
            values.AddRange(providers.Where(provider => provider != null && provider is not CompositeEntityModelMetadataProvider));
        if (SqlRuntimeFeatures.ReflectionFallbackEnabled && !generatedOnly)
        {
            values.Add(new GeneratedEntityModelMetadataProvider());
            _registeredProviderCount = values.Count;
            values.Add(new DataAnnotationsEntityModelMetadataProvider());
            values.Add(new ConventionEntityModelMetadataProvider());
        }
        else
        {
            _registeredProviderCount = values.Count;
        }
        _providers = values.AsReadOnly();
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType) => GetMetadata(entityType, _providers.Count);

    /// <summary>仅查询显式提供器和兼容模式下的全局生成模型，不触发默认反射回退。</summary>
    /// <param name="entityType">需要查询的实体类型。</param>
    /// <returns>匹配的元数据；没有登记时为 <see langword="null"/>。</returns>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public EntityModelMetadata GetRegisteredMetadata(Type entityType) => GetMetadata(entityType, _registeredProviderCount);

    private EntityModelMetadata GetMetadata(Type entityType, int count)
    {
        var registeredCount = Math.Min(count, _registeredProviderCount);
        for (var i = 0; i < registeredCount; i++)
        {
            var metadata = _providers[i].GetMetadata(entityType);
            if (metadata != null)
                return metadata;
        }

        if (entityType != null && registeredCount < count && _fallbackMetadata.TryGetValue(entityType, out var cached))
            return cached;
        for (var i = registeredCount; i < count; i++)
        {
            var metadata = _providers[i].GetMetadata(entityType);
            if (metadata != null)
                return entityType == null ? metadata : _fallbackMetadata.GetValue(entityType, _ => metadata);
        }
        return null;
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
}
