using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// Publishes complete mapping configurations atomically and exposes stable snapshots.
/// </summary>
public sealed class VersionedEntityMappingResolver : IEntityMappingResolver, IEntityMappingSnapshotProvider
{
    private readonly object _publishLock = new();
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly SqlMetadataOptions _baseOptions;
    private readonly ITypeConverterResolver _typeConverterResolver;
    private readonly IEntityModelMetadataProvider _metadataProvider;
    private Snapshot _current;

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

    /// <summary>Gets the currently published mapping version.</summary>
    public long CurrentVersion => Volatile.Read(ref _current).Version;

    /// <summary>Publishes a complete mapping configuration snapshot atomically.</summary>
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

    public IEntityMappingResolver CaptureSnapshot() => Volatile.Read(ref _current).Resolver;

    public EntityDescriptor GetDescriptor(Type entityType) => CaptureSnapshot().GetDescriptor(entityType);

    public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) =>
        CaptureSnapshot().Resolve(entityType, databaseContext);

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

    private sealed record Snapshot(long Version, IEntityMappingResolver Resolver);
}

