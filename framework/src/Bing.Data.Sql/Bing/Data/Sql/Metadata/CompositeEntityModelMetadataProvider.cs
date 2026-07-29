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

    /// <summary>
    /// 初始化默认组合提供器。
    /// </summary>
    public CompositeEntityModelMetadataProvider()
        : this(null)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="CompositeEntityModelMetadataProvider"/>类型的实例。
    /// </summary>
    /// <param name="providers">前置自定义提供器集合。</param>
    public CompositeEntityModelMetadataProvider(IEnumerable<IEntityModelMetadataProvider> providers)
    {
        var values = new List<IEntityModelMetadataProvider>();
        if (providers != null)
            values.AddRange(providers.Where(provider => provider != null && provider is not CompositeEntityModelMetadataProvider));
        values.Add(new DataAnnotationsEntityModelMetadataProvider());
        values.Add(new ConventionEntityModelMetadataProvider());
        _providers = values.AsReadOnly();
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType)
    {
        foreach (var provider in _providers)
        {
            var metadata = provider.GetMetadata(entityType);
            if (metadata != null)
                return metadata;
        }
        return null;
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
}