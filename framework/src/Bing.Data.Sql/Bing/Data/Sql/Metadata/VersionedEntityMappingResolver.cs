using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 按版本发布实体映射配置并提供稳定快照。
/// </summary>
/// <remarks>
/// 每次发布都会创建新的解析器快照，已有调用方继续使用其捕获的旧版本。
/// </remarks>
public sealed class VersionedEntityMappingResolver : IEntityMappingResolver, IEntityMappingSnapshotProvider
{
    /// <summary>
    /// 串行化映射版本发布的同步锁。
    /// </summary>
    private readonly object _publishLock = new();

    /// <summary>
    /// 用于解析当前数据库上下文的访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// 用于创建后续解析器快照的基础配置副本。
    /// </summary>
    private readonly SqlMetadataOptions _baseOptions;

    /// <summary>
    /// 用于解析实体值类型转换器的解析器。
    /// </summary>
    private readonly ITypeConverterResolver _typeConverterResolver;

    /// <summary>
    /// 用于读取实体模型元数据的提供程序。
    /// </summary>
    private readonly IEntityModelMetadataProvider _metadataProvider;

    /// <summary>
    /// 当前已发布的映射快照。
    /// </summary>
    private Snapshot _current;

    /// <summary>
    /// 初始化一个 <see cref="VersionedEntityMappingResolver"/> 类型的实例。
    /// </summary>
    /// <param name="databaseContextAccessor">用于解析当前数据库上下文的访问器。</param>
    /// <param name="options">初始实体映射及元数据配置；未提供时使用默认配置。</param>
    /// <param name="typeConverterResolver">用于解析类型转换器的解析器。</param>
    /// <param name="entityModelMetadataProvider">用于读取实体模型元数据的提供程序。</param>
    public VersionedEntityMappingResolver(IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions options = null, ITypeConverterResolver typeConverterResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null)
    {
        _databaseContextAccessor = databaseContextAccessor;
        var sourceOptions = options ?? new SqlMetadataOptions();
        _baseOptions = CloneBaseOptions(sourceOptions);
        _typeConverterResolver = typeConverterResolver;
        _metadataProvider = entityModelMetadataProvider;
        _current = new Snapshot(1, CreateResolver(sourceOptions.EntityMappings));
    }

    /// <summary>
    /// 获取当前已发布映射的版本号。
    /// </summary>
    public long CurrentVersion => Volatile.Read(ref _current).Version;

    /// <summary>
    /// 原子发布一组完整的实体映射配置。
    /// </summary>
    /// <param name="mappings">要发布的实体映射配置集合。</param>
    /// <returns>新发布的映射版本号。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mappings"/> 为 <see langword="null"/>。</exception>
    /// <remarks>发布成功后，后续捕获的快照使用新配置；已经捕获的快照保持不变。</remarks>
    public long PublishMappings(IEnumerable<EntityMappingOptions> mappings)
    {
        if (mappings == null)
            throw new ArgumentNullException(nameof(mappings));
        lock (_publishLock)
        {
            var resolver = CreateResolver(mappings);
            var version = checked(_current.Version + 1);
            Volatile.Write(ref _current, new Snapshot(version, resolver));
            return version;
        }
    }

    /// <inheritdoc />
    public IEntityMappingResolver CaptureSnapshot() => Volatile.Read(ref _current).Resolver;

    /// <inheritdoc />
    public EntityDescriptor GetDescriptor(Type entityType) => CaptureSnapshot().GetDescriptor(entityType);

    /// <inheritdoc />
    public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) =>
        CaptureSnapshot().Resolve(entityType, databaseContext);

    /// <summary>
    /// 复制用于后续版本的元数据基础配置。
    /// </summary>
    /// <param name="source">待复制的元数据配置。</param>
    /// <returns>包含基础选项副本的配置对象。</returns>
    private static SqlMetadataOptions CloneBaseOptions(SqlMetadataOptions source) => new()
    {
        EntityMappingCacheCapacity = source.EntityMappingCacheCapacity,
        EntityMappingCacheEvictionPolicy = source.EntityMappingCacheEvictionPolicy,
        MutationPlanCacheCapacity = source.MutationPlanCacheCapacity,
        MutationGetterCacheCapacity = source.MutationGetterCacheCapacity,
        DefaultDatabaseContext = DatabaseContextSnapshot.Create(source.DefaultDatabaseContext),
        StrictMetadata = source.StrictMetadata,
        BoolTrueValue = source.BoolTrueValue,
        BoolFalseValue = source.BoolFalseValue
    };

    /// <summary>
    /// 根据映射集合创建独立的实体映射解析器。
    /// </summary>
    /// <param name="mappings">新解析器使用的实体映射集合。</param>
    /// <returns>配置完成的实体映射解析器。</returns>
    private DefaultEntityMappingResolver CreateResolver(IEnumerable<EntityMappingOptions> mappings)
    {
        var options = new SqlMetadataOptions
        {
            EntityMappingCacheCapacity = _baseOptions.EntityMappingCacheCapacity,
            EntityMappingCacheEvictionPolicy = _baseOptions.EntityMappingCacheEvictionPolicy,
            MutationPlanCacheCapacity = _baseOptions.MutationPlanCacheCapacity,
            MutationGetterCacheCapacity = _baseOptions.MutationGetterCacheCapacity,
            DefaultDatabaseContext = DatabaseContextSnapshot.Create(_baseOptions.DefaultDatabaseContext),
            StrictMetadata = _baseOptions.StrictMetadata,
            BoolTrueValue = _baseOptions.BoolTrueValue,
            BoolFalseValue = _baseOptions.BoolFalseValue
        };
        foreach (var mapping in DefaultEntityMappingResolver.CloneMappings(mappings))
            options.EntityMappings.Add(mapping);
        return new DefaultEntityMappingResolver(_databaseContextAccessor, options, _typeConverterResolver, _metadataProvider);
    }

    /// <summary>
    /// 保存一个已发布的映射版本及其解析器。
    /// </summary>
    /// <param name="Version">映射版本号。</param>
    /// <param name="Resolver">该版本对应的实体映射解析器。</param>
    private sealed record Snapshot(long Version, IEntityMappingResolver Resolver);
}

