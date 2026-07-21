using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    /// 实体描述缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, EntityDescriptor> DescriptorCache = new();

    /// <summary>
    /// 实体映射缓存
    /// </summary>
    private readonly ConcurrentDictionary<EntityMappingCacheKey, EntityMappingMetadata> _mappingCache = new();

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
        _entityModelMetadataProvider = entityModelMetadataProvider ?? new DefaultEntityModelMetadataProvider();
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
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
    /// <param name="entityType">实体类型</param>
    /// <returns>实体描述信息</returns>
    public EntityDescriptor GetDescriptor(Type entityType)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        return DescriptorCache.GetOrAdd(entityType, CreateDescriptor);
    }

    /// <summary>
    /// 解析实体映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>实体映射元数据</returns>
    public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        var context = GetDatabaseContext(databaseContext);
        var mappingOptions = ResolveEntityMappingOptions(entityType, context);
        var cacheKey = new EntityMappingCacheKey(
            entityType.TypeHandle,
            NormalizeCacheValue(context.DbKey),
            GetDatabaseType(context),
            NormalizeCacheValue(GetMappingProfile(context, mappingOptions)),
            GetCacheTableRouteKey(mappingOptions));
        if (_mappingCache.TryGetValue(cacheKey, out var cachedMapping))
            return cachedMapping;
        var schema = GetSchema(entityType, mappingOptions);
        var tableName = GetTableName(entityType, mappingOptions);
        return _mappingCache.GetOrAdd(cacheKey,
            _ => CreateMapping(entityType, context, schema, tableName, mappingOptions));
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
    /// <param name="entityType">实体类型</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>数据库架构。</returns>
    protected virtual string GetSchema(Type entityType, EntityMappingOptions mappingOptions)
    {
        if (string.IsNullOrWhiteSpace(mappingOptions?.Schema) == false)
            return mappingOptions.Schema;
        return _entityModelMetadataProvider.GetSchema(entityType) ?? string.Empty;
    }

    /// <summary>
    /// 获取表名
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>表名</returns>
    protected virtual string GetTableName(Type entityType, EntityMappingOptions mappingOptions) =>
        string.IsNullOrWhiteSpace(mappingOptions?.TableName)
            ? _entityModelMetadataProvider.GetTableName(entityType) ?? entityType.Name
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
    /// 创建实体描述信息
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体描述信息</returns>
    protected virtual EntityDescriptor CreateDescriptor(Type entityType)
    {
        var properties = entityType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(t => t.CanRead && t.GetIndexParameters().Length == 0)
            .Where(t => t.GetCustomAttribute<NotMappedAttribute>() == null)
            .ToList();
        var keyProperties = properties
            .Where(IsKeyProperty)
            .ToList();
        return new EntityDescriptor
        {
            EntityType = entityType,
            Properties = properties,
            KeyProperties = keyProperties
        };
    }

    /// <summary>
    /// 创建实体映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="schema">架构</param>
    /// <param name="tableName">表名</param>
    /// <param name="mappingOptions">实体映射配置</param>
    /// <returns>实体映射元数据</returns>
    protected virtual EntityMappingMetadata CreateMapping(Type entityType, DatabaseContext databaseContext, string schema,
        string tableName, EntityMappingOptions mappingOptions)
    {
        var descriptor = GetDescriptor(entityType);
        var columns = new ReadOnlyDictionary<string, ColumnMappingMetadata>(descriptor.Properties.ToDictionary(
            t => t.Name,
            t => CreateColumnMetadata(entityType, t, GetColumnMappingOptions(mappingOptions, t), databaseContext),
            StringComparer.OrdinalIgnoreCase));
        var tableReference = new SqlTableReference
        {
            EntityType = entityType,
            Database = mappingOptions?.Database,
            Schema = schema,
            TableName = tableName,
        };
        return new EntityMappingMetadata
        {
            EntityType = entityType,
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
    private static string NormalizeCacheValue(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

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
    /// <param name="entityType">实体类型</param>
    /// <param name="property">属性信息</param>
    /// <param name="mappingOptions">列映射配置</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata CreateColumnMetadata(Type entityType, PropertyInfo property,
        ColumnMappingOptions mappingOptions, DatabaseContext databaseContext)
    {
        var propertyType = GetUnderlyingType(property.PropertyType);
        var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
        var providerTypeName = string.IsNullOrWhiteSpace(mappingOptions?.ProviderTypeName)
            ? columnAttribute?.TypeName
            : mappingOptions.ProviderTypeName;
        var size = mappingOptions?.Size ?? GetSize(property);
        var storageKind = mappingOptions == null || mappingOptions.StorageKind == ColumnStorageKind.Default
            ? GetStorageKind(propertyType, providerTypeName)
            : mappingOptions.StorageKind;
        var converterKind = mappingOptions == null || mappingOptions.ConverterKind == FieldValueConverterKind.None
            ? GetConverterKind(propertyType, storageKind)
            : mappingOptions.ConverterKind;
        var columnName = string.IsNullOrWhiteSpace(mappingOptions?.ColumnName)
            ? _entityModelMetadataProvider.GetColumnName(entityType, property.Name) ?? property.Name
            : mappingOptions.ColumnName;
        var column = new ColumnMappingMetadata
        {
            PropertyName = string.IsNullOrWhiteSpace(mappingOptions?.PropertyName) ? property.Name : mappingOptions.PropertyName,
            ColumnName = columnName,
            Column = new ColumnIdentifier(columnName),
            ClrType = property.PropertyType,
            DbType = mappingOptions?.DbType ?? GetDbType(propertyType, providerTypeName, size,
                GetDatabaseType(databaseContext)),
            Size = size,
            Precision = mappingOptions?.Precision ?? GetPrecision(providerTypeName),
            Scale = mappingOptions?.Scale ?? GetScale(providerTypeName),
            ProviderTypeName = providerTypeName,
            IsNullable = IsNullable(property.PropertyType),
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
    /// 判断是否为主键属性
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <returns>是否为主键属性</returns>
    protected virtual bool IsKeyProperty(PropertyInfo property)
    {
        if (property.GetCustomAttribute<KeyAttribute>() != null)
            return true;
        if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            return true;
        var declaringType = property.DeclaringType;
        return declaringType != null && property.Name.Equals($"{declaringType.Name}Id", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取实际 CLR 类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>实际 CLR 类型</returns>
    protected virtual Type GetUnderlyingType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// 获取长度
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <returns>长度</returns>
    protected virtual int? GetSize(PropertyInfo property)
    {
        var maxLength = property.GetCustomAttribute<MaxLengthAttribute>()?.Length;
        if (maxLength > 0)
            return maxLength;
        var stringLength = property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
        return stringLength > 0 ? stringLength : null;
    }

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
