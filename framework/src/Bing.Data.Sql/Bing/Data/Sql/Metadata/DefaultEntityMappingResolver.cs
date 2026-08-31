using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认实体映射解析器
/// </summary>
public class DefaultEntityMappingResolver : IEntityMappingResolver
{
    /// <summary>
    /// 实体映射缓存
    /// </summary>
    private readonly ConcurrentDictionary<EntityMappingCacheKey, EntityMappingMetadata> _mappingCache = new();

    /// <summary>
    /// 最近最少使用策略下按访问先后保存的最终映射缓存键。
    /// </summary>
    private readonly LinkedList<EntityMappingCacheKey> _mappingCacheAccessOrder = new();

    /// <summary>
    /// 最近最少使用策略下的缓存键节点索引。
    /// </summary>
    private readonly Dictionary<EntityMappingCacheKey, LinkedListNode<EntityMappingCacheKey>> _mappingCacheAccessNodes = new();

    /// <summary>
    /// 控制有限容量映射缓存准入的同步锁。
    /// </summary>
    private readonly object _mappingCacheAdmissionLock = new();

    /// <summary>
    /// 最终实体映射缓存的固定容量；<see langword="null"/> 表示不限制容量。
    /// </summary>
    private readonly int? _mappingCacheCapacity;

    /// <summary>
    /// 最终映射缓存达到容量后的固定处理策略。
    /// </summary>
    private readonly EntityMappingCacheEvictionPolicy _mappingCacheEvictionPolicy;

    /// <summary>
    /// 最终实体映射缓存首次查找命中次数。
    /// </summary>
    private long _mappingCacheHitCount;

    /// <summary>
    /// 最终实体映射缓存首次查找未命中次数。
    /// </summary>
    private long _mappingCacheMissCount;

    /// <summary>
    /// 最终实体映射因容量策略未写入缓存的次数。
    /// </summary>
    private long _mappingCacheBypassCount;

    /// <summary>
    /// 最终映射缓存按最近最少使用策略淘汰的次数。
    /// </summary>
    private long _mappingCacheEvictionCount;

    /// <summary>
    /// 按实体类型缓存原始模型元数据，避免为计算最终缓存键重复调用外部 ORM 元数据提供器。
    /// </summary>
    private readonly ConcurrentDictionary<RuntimeTypeHandle, Lazy<EntityModelMetadata>> _modelCache = new();

    /// <summary>
    /// 按实体类型分组并按匹配优先级排序的映射配置索引。
    /// </summary>
    private readonly IReadOnlyDictionary<RuntimeTypeHandle, EntityMappingOptions[]> _mappingOptionsIndex;

    /// <summary>
    /// 实体模型元数据提供器
    /// </summary>
    private readonly IEntityModelMetadataProvider _entityModelMetadataProvider;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 数据类型转换器
    /// </summary>
    private readonly ITypeConverterResolver _typeConverterResolver;

    /// <summary>
    /// 获取当前最终实体映射缓存的条目数。
    /// </summary>
    internal int MappingCacheCount => _mappingCache.Count;

    /// <summary>
    /// 获取最终实体映射缓存的聚合统计快照。
    /// </summary>
    internal EntityMappingCacheStatistics MappingCacheStatistics => new(
        System.Threading.Interlocked.Read(ref _mappingCacheHitCount),
        System.Threading.Interlocked.Read(ref _mappingCacheMissCount),
        System.Threading.Interlocked.Read(ref _mappingCacheBypassCount),
        System.Threading.Interlocked.Read(ref _mappingCacheEvictionCount),
        _mappingCache.Count,
        _mappingCacheCapacity,
        _mappingCacheEvictionPolicy);

    /// <summary>
    /// 初始化一个<see cref="DefaultEntityMappingResolver"/>类型的实例
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器</param>
    public DefaultEntityMappingResolver(IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions options = null,
        ITypeConverterResolver typeConverterResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null)
    {
        _entityModelMetadataProvider = entityModelMetadataProvider ?? new CompositeEntityModelMetadataProvider();
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
        _mappingCacheCapacity = _options.EntityMappingCacheCapacity;
        _mappingCacheEvictionPolicy = _options.EntityMappingCacheEvictionPolicy;
        if (_mappingCacheCapacity.HasValue && _mappingCacheCapacity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(SqlMetadataOptions.EntityMappingCacheCapacity),
                _mappingCacheCapacity, "实体最终映射缓存容量不能小于 0。");
        if (Enum.IsDefined(typeof(EntityMappingCacheEvictionPolicy), _mappingCacheEvictionPolicy) == false)
            throw new ArgumentOutOfRangeException(nameof(SqlMetadataOptions.EntityMappingCacheEvictionPolicy),
                _mappingCacheEvictionPolicy, "实体最终映射缓存淘汰策略无效。");
        _typeConverterResolver = typeConverterResolver ?? new DefaultTypeConverterResolver();
        _mappingOptionsIndex = CreateMappingOptionsIndex(_options.EntityMappings);
    }

    /// <summary>
    /// 使用实体模型元数据提供器初始化实体映射解析器。
    /// </summary>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="options">SQL 元数据配置。</param>
    public DefaultEntityMappingResolver(IEntityModelMetadataProvider entityModelMetadataProvider,
        IDatabaseContextAccessor databaseContextAccessor = null, SqlMetadataOptions options = null)
        : this(databaseContextAccessor, options, entityModelMetadataProvider: entityModelMetadataProvider)
    {
    }

    /// <summary>
    /// 获取实体描述信息
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体描述信息</returns>
    public EntityDescriptor GetDescriptor(Type entityType)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        var model = GetModelMetadata(entityType);
        var properties = model.Properties.Values
            .Where(property => property.IsIgnored == false)
            .Select(property => property.Property)
            .ToList();
        return new EntityDescriptor
        {
            EntityType = entityType,
            Properties = properties,
            KeyProperties = model.Properties.Values
                .Where(property => property.IsIgnored == false && property.IsKey)
                .Select(property => property.Property)
                .ToList()
        };
    }

    /// <summary>
    /// 解析实体映射元数据
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>实体映射元数据</returns>
    public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        var context = GetDatabaseContext(databaseContext);
        var mappingOptions = ResolveEntityMappingOptions(entityType, context);
        var model = GetCachedModelMetadata(entityType);
        var database = mappingOptions?.Database;
        var schema = GetSchema(model, mappingOptions);
        var tableName = GetTableName(model, mappingOptions);
        var cacheKey = new EntityMappingCacheKey(
            entityType.TypeHandle,
            NormalizeCacheValue(context.DbKey),
            GetDatabaseType(context),
            NormalizeCacheValue(GetMappingProfile(context, mappingOptions)),
            GetCacheTableRouteKey(mappingOptions),
            NormalizeCacheValue(database),
            NormalizeCacheValue(schema),
            NormalizeCacheValue(tableName));
        if (_mappingCache.TryGetValue(cacheKey, out var cachedMapping))
        {
            System.Threading.Interlocked.Increment(ref _mappingCacheHitCount);
            TouchMappingCacheEntry(cacheKey);
            return cachedMapping;
        }
        System.Threading.Interlocked.Increment(ref _mappingCacheMissCount);
        return GetOrCreateMapping(cacheKey, model, context, schema, tableName, mappingOptions);
    }

    /// <summary>
    /// 获取或创建最终实体映射，并在配置了容量时按固定策略限制新条目的准入。
    /// </summary>
    /// <param name="cacheKey">根据最终路由结果生成的映射缓存键。</param>
    /// <param name="model">已缓存的实体模型元数据。</param>
    /// <param name="databaseContext">当前数据库上下文。</param>
    /// <param name="schema">最终数据库架构。</param>
    /// <param name="tableName">最终物理表名。</param>
    /// <param name="mappingOptions">已匹配的实体映射配置。</param>
    /// <returns>与当前路由匹配的实体映射元数据。</returns>
    private EntityMappingMetadata GetOrCreateMapping(EntityMappingCacheKey cacheKey, EntityModelMetadata model,
        DatabaseContext databaseContext, string schema, string tableName, EntityMappingOptions mappingOptions)
    {
        if (_mappingCacheCapacity == null)
            return _mappingCache.GetOrAdd(cacheKey,
                _ => CreateMapping(model, databaseContext, schema, tableName, mappingOptions));
        if (_mappingCacheCapacity.Value == 0)
        {
            System.Threading.Interlocked.Increment(ref _mappingCacheBypassCount);
            return CreateMapping(model, databaseContext, schema, tableName, mappingOptions);
        }

        lock (_mappingCacheAdmissionLock)
        {
            if (_mappingCache.TryGetValue(cacheKey, out var cachedMapping))
            {
                TouchMappingCacheEntryCore(cacheKey);
                return cachedMapping;
            }

            if (_mappingCache.Count >= _mappingCacheCapacity.Value)
            {
                if (_mappingCacheEvictionPolicy == EntityMappingCacheEvictionPolicy.AdmissionOnly)
                {
                    System.Threading.Interlocked.Increment(ref _mappingCacheBypassCount);
                    return CreateMapping(model, databaseContext, schema, tableName, mappingOptions);
                }
                EvictLeastRecentlyUsedMapping();
            }

            var mapping = CreateMapping(model, databaseContext, schema, tableName, mappingOptions);
            _mappingCache.TryAdd(cacheKey, mapping);
            AddMappingCacheEntryToAccessOrder(cacheKey);
            return mapping;
        }
    }

    /// <summary>
    /// 在最近最少使用策略下将指定缓存项标记为最新访问。
    /// </summary>
    /// <param name="cacheKey">待更新访问顺序的映射缓存键。</param>
    private void TouchMappingCacheEntry(EntityMappingCacheKey cacheKey)
    {
        if (_mappingCacheEvictionPolicy != EntityMappingCacheEvictionPolicy.LeastRecentlyUsed ||
            _mappingCacheCapacity.HasValue == false || _mappingCacheCapacity.Value == 0)
            return;
        lock (_mappingCacheAdmissionLock)
            TouchMappingCacheEntryCore(cacheKey);
    }

    /// <summary>
    /// 在已持有准入锁时将指定缓存项标记为最新访问。
    /// </summary>
    /// <param name="cacheKey">待更新访问顺序的映射缓存键。</param>
    private void TouchMappingCacheEntryCore(EntityMappingCacheKey cacheKey)
    {
        if (_mappingCacheEvictionPolicy != EntityMappingCacheEvictionPolicy.LeastRecentlyUsed ||
            _mappingCacheAccessNodes.TryGetValue(cacheKey, out var node) == false)
            return;
        _mappingCacheAccessOrder.Remove(node);
        _mappingCacheAccessOrder.AddLast(node);
    }

    /// <summary>
    /// 在已持有准入锁时登记最新缓存项。
    /// </summary>
    /// <param name="cacheKey">待登记访问顺序的映射缓存键。</param>
    private void AddMappingCacheEntryToAccessOrder(EntityMappingCacheKey cacheKey)
    {
        if (_mappingCacheEvictionPolicy != EntityMappingCacheEvictionPolicy.LeastRecentlyUsed)
            return;
        var node = _mappingCacheAccessOrder.AddLast(cacheKey);
        _mappingCacheAccessNodes.Add(cacheKey, node);
    }

    /// <summary>
    /// 在已持有准入锁时移除最近最少使用的最终映射项。
    /// </summary>
    private void EvictLeastRecentlyUsedMapping()
    {
        var node = _mappingCacheAccessOrder.First;
        if (node == null)
            return;
        _mappingCacheAccessOrder.RemoveFirst();
        _mappingCacheAccessNodes.Remove(node.Value);
        _mappingCache.TryRemove(node.Value, out _);
        System.Threading.Interlocked.Increment(ref _mappingCacheEvictionCount);
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>数据库上下文</returns>
    protected virtual DatabaseContext GetDatabaseContext(DatabaseContext databaseContext)
    {
        if (databaseContext != null)
            return databaseContext;
        if (_databaseContextAccessor?.Current != null)
            return _databaseContextAccessor.Current;
        if (_options.DefaultDatabaseContext != null)
            return _options.DefaultDatabaseContext;
        return new DatabaseContext();
    }

    /// <summary>
    /// 获取表路由键
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>表路由键</returns>
    protected virtual string GetTableRouteKey(DatabaseContext databaseContext,
        EntityMappingOptions mappingOptions = null) =>
        string.IsNullOrWhiteSpace(mappingOptions?.TableRouteKey)
            ? databaseContext?.TenantId ?? string.Empty
            : mappingOptions.TableRouteKey;

    /// <summary>
    /// 获取映射配置名称
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>映射配置名称</returns>
    protected virtual string GetMappingProfile(DatabaseContext databaseContext,
        EntityMappingOptions mappingOptions = null) =>
        string.IsNullOrWhiteSpace(databaseContext?.MappingProfile)
            ? mappingOptions?.MappingProfile ?? string.Empty
            : databaseContext.MappingProfile;

    /// <summary>
    /// 获取数据库架构。
    /// </summary>
    /// <param name="model">实体模型元数据。</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>数据库架构。</returns>
    protected virtual string GetSchema(EntityModelMetadata model, EntityMappingOptions mappingOptions)
    {
        if (string.IsNullOrWhiteSpace(mappingOptions?.Schema) == false)
            return mappingOptions.Schema;
        return model.Schema ?? string.Empty;
    }

    /// <summary>
    /// 获取表名
    /// </summary>
    /// <param name="model">实体模型元数据。</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>表名</returns>
    protected virtual string GetTableName(EntityModelMetadata model, EntityMappingOptions mappingOptions) =>
        string.IsNullOrWhiteSpace(mappingOptions?.TableName)
            ? model.TableName ?? model.EntityType.Name
            : mappingOptions.TableName;

    /// <summary>
    /// 解析实体映射配置
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>实体映射配置</returns>
    protected virtual EntityMappingOptions ResolveEntityMappingOptions(Type entityType, DatabaseContext databaseContext)
    {
        var routeKey = GetTableRouteKey(databaseContext);
        var mappingProfile = GetMappingProfile(databaseContext);
        if (_mappingOptionsIndex.TryGetValue(entityType.TypeHandle, out var candidates) == false)
            return null;
        var databaseType = GetDatabaseType(databaseContext);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.DbKey) == false &&
                string.Equals(candidate.DbKey, databaseContext?.DbKey, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            if (candidate.DatabaseType != null && candidate.DatabaseType != databaseType)
                continue;
            if (string.IsNullOrWhiteSpace(candidate.MappingProfile) == false &&
                string.Equals(candidate.MappingProfile, mappingProfile, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            if (string.IsNullOrWhiteSpace(candidate.TableRouteKey) == false &&
                string.Equals(candidate.TableRouteKey, routeKey, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            return candidate;
        }
        return null;
    }

    /// <summary>
    /// 创建按实体类型分组的映射配置只读索引。
    /// </summary>
    /// <param name="mappings">原始实体映射配置集合。</param>
    /// <returns>按实体类型索引的配置集合。</returns>
    private IReadOnlyDictionary<RuntimeTypeHandle, EntityMappingOptions[]> CreateMappingOptionsIndex(
        IEnumerable<EntityMappingOptions> mappings)
    {
        var groups = new Dictionary<RuntimeTypeHandle, List<EntityMappingOptions>>();
        if (mappings != null)
        {
            foreach (var mapping in mappings)
            {
                if (mapping?.EntityType == null)
                    continue;
                var typeHandle = mapping.EntityType.TypeHandle;
                if (groups.TryGetValue(typeHandle, out var candidates) == false)
                {
                    candidates = new List<EntityMappingOptions>();
                    groups.Add(typeHandle, candidates);
                }
                candidates.Add(mapping);
            }
        }

        var result = new Dictionary<RuntimeTypeHandle, EntityMappingOptions[]>(groups.Count);
        foreach (var group in groups)
            result.Add(group.Key, group.Value.OrderByDescending(GetMappingSpecificity).ToArray());
        return new ReadOnlyDictionary<RuntimeTypeHandle, EntityMappingOptions[]>(result);
    }

    /// <summary>
    /// 获取实体映射配置优先级
    /// </summary>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>优先级</returns>
    protected virtual int GetMappingSpecificity(EntityMappingOptions mappingOptions)
    {
        if (mappingOptions == null)
            return 0;
        var result = 0;
        if (string.IsNullOrWhiteSpace(mappingOptions.DbKey) == false)
            result += 4;
        if (mappingOptions.DatabaseType != null)
            result += 8;
        if (string.IsNullOrWhiteSpace(mappingOptions.MappingProfile) == false)
            result += 2;
        if (string.IsNullOrWhiteSpace(mappingOptions.TableRouteKey) == false)
            result += 1;
        return result;
    }

    /// <summary>
    /// 创建实体映射元数据
    /// </summary>
    /// <param name="model">实体模型元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="schema">架构</param>
    /// <param name="tableName">表名</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>实体映射元数据</returns>
    protected virtual EntityMappingMetadata CreateMapping(EntityModelMetadata model, DatabaseContext databaseContext, string schema,
        string tableName, EntityMappingOptions mappingOptions)
    {
        var columns = new ReadOnlyDictionary<string, ColumnMappingMetadata>(model.Properties.Values
            .Where(property => property.IsIgnored == false)
            .ToDictionary(property => property.PropertyName,
            property => CreateColumnMetadata(property, GetColumnMappingOptions(mappingOptions, property.Property), databaseContext),
            StringComparer.OrdinalIgnoreCase));
        var tableReference = new SqlTableReference
        {
            EntityType = model.EntityType,
            Database = mappingOptions?.Database,
            Schema = schema,
            TableName = tableName,
        };
        return new EntityMappingMetadata
        {
            EntityType = model.EntityType,
            Model = model,
            MappingProfile = GetMappingProfile(databaseContext, mappingOptions),
            Table = tableReference,
            TableRouteKey = GetCacheTableRouteKey(mappingOptions),
            Columns = columns
        };
    }

    /// <summary>
    /// 规范化映射缓存键中的可选字符串。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <returns>规范化后的缓存键值。</returns>
    private static string NormalizeCacheValue(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().ToUpperInvariant();

    /// <summary>
    /// 获取不包含租户标识的映射缓存路由键。
    /// </summary>
    /// <param name="mappingOptions">已匹配的实体映射配置。</param>
    /// <returns>可安全写入缓存的路由键。</returns>
    private static string GetCacheTableRouteKey(EntityMappingOptions mappingOptions) =>
        NormalizeCacheValue(mappingOptions?.TableRouteKey);

    /// <summary>
    /// 获取列映射配置
    /// </summary>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <param name="property">属性信息</param>
    /// <returns>列映射配置</returns>
    protected virtual ColumnMappingOptions GetColumnMappingOptions(EntityMappingOptions mappingOptions,
        PropertyInfo property)
    {
        if (mappingOptions?.Columns == null || property == null)
            return null;
        if (mappingOptions.Columns.TryGetValue(property.Name, out var result))
            return result;
        return mappingOptions.Columns.Values.FirstOrDefault(t =>
            string.Equals(t?.PropertyName, property.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 创建列映射元数据
    /// </summary>
    /// <param name="property">实体属性元数据</param>
    /// <param name="mappingOptions">列映射配置</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata CreateColumnMetadata(EntityPropertyMetadata property,
        ColumnMappingOptions mappingOptions, DatabaseContext databaseContext)
    {
        var propertyType = GetUnderlyingType(property.ClrType);
        var providerTypeName = string.IsNullOrWhiteSpace(mappingOptions?.ProviderTypeName)
            ? property.ProviderTypeName
            : mappingOptions.ProviderTypeName;
        var size = mappingOptions?.Size ?? property.MaxLength;
        var storageKind = mappingOptions == null || mappingOptions.StorageKind == ColumnStorageKind.Default
            ? GetStorageKind(propertyType, providerTypeName)
            : mappingOptions.StorageKind;
        var converterKind = mappingOptions == null || mappingOptions.ConverterKind == FieldValueConverterKind.None
            ? GetConverterKind(propertyType, storageKind)
            : mappingOptions.ConverterKind;
        var columnName = string.IsNullOrWhiteSpace(mappingOptions?.ColumnName)
            ? property.ColumnName
            : mappingOptions.ColumnName;
        var column = new ColumnMappingMetadata
        {
            PropertyName = string.IsNullOrWhiteSpace(mappingOptions?.PropertyName) ? property.PropertyName : mappingOptions.PropertyName,
            ColumnName = columnName,
            Column = new ColumnIdentifier(columnName),
            ClrType = property.ClrType,
            DbType = mappingOptions?.DbType ?? GetDbType(propertyType, providerTypeName, size,
                GetDatabaseType(databaseContext)),
            Size = size,
            Precision = mappingOptions?.Precision ?? GetPrecision(providerTypeName),
            Scale = mappingOptions?.Scale ?? GetScale(providerTypeName),
            ProviderTypeName = providerTypeName,
            IsNullable = property.IsNullable,
            IsKey = property.IsKey,
            IsDatabaseGenerated = property.IsDatabaseGenerated,
            IsConcurrencyToken = property.IsConcurrencyToken,
            CanInsert = property.CanInsert,
            CanUpdate = property.CanUpdate,
            StorageKind = storageKind,
            ConverterKind = converterKind,
            CustomConverterName = mappingOptions?.CustomConverterName
        };
        return column;
    }

    /// <summary>
    /// 获取数据库类型
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>数据库类型</returns>
    protected virtual DatabaseType? GetDatabaseType(DatabaseContext databaseContext) =>
        databaseContext?.DataSource?.DatabaseType;

    /// <summary>
    /// 获取实际 CLR 类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>实际 CLR 类型</returns>
    protected virtual Type GetUnderlyingType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// 获取精度
    /// </summary>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <returns>精度</returns>
    protected virtual byte? GetPrecision(string providerTypeName)
    {
        var (precision, _) = ParsePrecision(providerTypeName);
        return precision;
    }

    /// <summary>
    /// 获取小数位
    /// </summary>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <returns>小数位</returns>
    protected virtual byte? GetScale(string providerTypeName)
    {
        var (_, scale) = ParsePrecision(providerTypeName);
        return scale;
    }

    /// <summary>
    /// 解析精度与小数位
    /// </summary>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <returns>精度与小数位</returns>
    protected virtual (byte? Precision, byte? Scale) ParsePrecision(string providerTypeName)
    {
        if (string.IsNullOrWhiteSpace(providerTypeName))
            return (null, null);
        var match = Regex.Match(providerTypeName, @"\((?<precision>\d+)\s*,\s*(?<scale>\d+)\)");
        if (match.Success == false)
            return (null, null);
        return (byte.Parse(match.Groups["precision"].Value), byte.Parse(match.Groups["scale"].Value));
    }

    /// <summary>
    /// 获取 DbType
    /// </summary>
    /// <param name="type">CLR 类型</param>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <param name="length">长度</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>DbType</returns>
    protected virtual DbType? GetDbType(Type type, string providerTypeName, int? length, DatabaseType? databaseType)
    {
        var dbType = databaseType == null
            ? null
            : _typeConverterResolver.Resolve(databaseType.Value)?.ToDbType(GetProviderDataTypeName(providerTypeName), length);
        if (dbType != null)
            return dbType;
        if (type.IsEnum)
            return GetDbType(Enum.GetUnderlyingType(type), providerTypeName, length, databaseType);
        if (type == typeof(string))
            return DbType.String;
        if (type == typeof(bool))
            return DbType.Boolean;
        if (type == typeof(byte))
            return DbType.Byte;
        if (type == typeof(short))
            return DbType.Int16;
        if (type == typeof(int))
            return DbType.Int32;
        if (type == typeof(long))
            return DbType.Int64;
        if (type == typeof(float))
            return DbType.Single;
        if (type == typeof(double))
            return DbType.Double;
        if (type == typeof(decimal))
            return DbType.Decimal;
        if (type == typeof(Guid))
            return DbType.Guid;
        if (type == typeof(DateTime))
            return DbType.DateTime;
        if (type == typeof(DateTimeOffset))
            return DbType.DateTimeOffset;
        if (type == typeof(byte[]))
            return DbType.Binary;
        if (type == typeof(char))
            return DbType.StringFixedLength;
        return null;
    }

    /// <summary>
    /// 获取 Provider 数据类型名称
    /// </summary>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <returns>数据类型名称</returns>
    protected virtual string GetProviderDataTypeName(string providerTypeName)
    {
        if (string.IsNullOrWhiteSpace(providerTypeName))
            return providerTypeName;
        var index = providerTypeName.IndexOf('(');
        return index < 0 ? providerTypeName : providerTypeName.Substring(0, index).Trim();
    }

    /// <summary>
    /// 判断是否可空
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否可空</returns>
    protected virtual bool IsNullable(Type type)
    {
        if (type == null)
            return true;
        if (type.IsValueType == false)
            return true;
        return Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// 获取实体模型元数据，未处理时回退到命名约定模型。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体模型元数据。</returns>
    protected virtual EntityModelMetadata GetModelMetadata(Type entityType) =>
        _entityModelMetadataProvider.GetMetadata(entityType) ??
        new ConventionEntityModelMetadataProvider().GetMetadata(entityType);

    /// <summary>
    /// 获取实体模型元数据缓存。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>用于计算最终映射对象名的稳定模型元数据。</returns>
    private EntityModelMetadata GetCachedModelMetadata(Type entityType) => _modelCache.GetOrAdd(entityType.TypeHandle,
        _ => new Lazy<EntityModelMetadata>(() => GetModelMetadata(entityType),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>
    /// 获取字段存储方式
    /// </summary>
    /// <param name="type">CLR 类型</param>
    /// <param name="providerTypeName">Provider 数据类型名称</param>
    /// <returns>字段存储方式</returns>
    protected virtual ColumnStorageKind GetStorageKind(Type type, string providerTypeName)
    {
        if (string.Equals(providerTypeName, "json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerTypeName, "jsonb", StringComparison.OrdinalIgnoreCase))
            return ColumnStorageKind.Json;
        if (type.IsEnum)
            return ColumnStorageKind.EnumValue;
        if (type == typeof(string))
            return ColumnStorageKind.String;
        if (type == typeof(bool))
            return ColumnStorageKind.Boolean;
        if (type == typeof(Guid))
            return ColumnStorageKind.Guid;
        if (type == typeof(byte[]))
            return ColumnStorageKind.Binary;
        if (type == typeof(DateTime))
            return ColumnStorageKind.DateTime;
        if (type == typeof(DateTimeOffset))
            return ColumnStorageKind.DateTimeOffset;
        if (type.IsPrimitive || type == typeof(decimal))
            return ColumnStorageKind.Number;
        return ColumnStorageKind.Default;
    }

    /// <summary>
    /// 获取字段值转换器类型
    /// </summary>
    /// <param name="type">CLR 类型</param>
    /// <param name="storageKind">字段存储方式</param>
    /// <returns>字段值转换器类型</returns>
    protected virtual FieldValueConverterKind GetConverterKind(Type type, ColumnStorageKind storageKind)
    {
        if (type.IsEnum)
        {
            return storageKind == ColumnStorageKind.EnumName
                ? FieldValueConverterKind.EnumToName
                : FieldValueConverterKind.EnumToValue;
        }
        return FieldValueConverterKind.None;
    }
}
