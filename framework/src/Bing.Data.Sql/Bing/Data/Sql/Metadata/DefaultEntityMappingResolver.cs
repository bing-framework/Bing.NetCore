using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Bing.Data;
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
    /// 实体元数据解析器
    /// </summary>
    private readonly IEntityMetadata _entityMetadata;

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
    private readonly ITypeConverter _typeConverter;

    /// <summary>
    /// 初始化一个<see cref="DefaultEntityMappingResolver"/>类型的实例
    /// </summary>
    /// <param name="entityMetadata">实体元数据解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="typeConverter">数据类型转换器</param>
    public DefaultEntityMappingResolver(IEntityMetadata entityMetadata = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions options = null,
        ITypeConverter typeConverter = null)
    {
        _entityMetadata = entityMetadata ?? new DefaultEntityMetadata();
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
        _typeConverter = typeConverter;
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
        var schema = _entityMetadata.GetSchema(entityType) ?? string.Empty;
        var tableName = _entityMetadata.GetTable(entityType) ?? entityType.Name;
        var cacheKey = new EntityMappingCacheKey(
            entityType,
            context.DbKey,
            context.DatabaseType,
            context.Role,
            schema,
            GetTableRouteKey(context),
            context.MappingVersion);
        return _mappingCache.GetOrAdd(cacheKey, _ => CreateMapping(entityType, context, schema, tableName));
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
        return new DatabaseContext
        {
            DbKey = ConnectionStringCollection.DefaultConnectionStringName,
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Default
        };
    }

    /// <summary>
    /// 获取表路由键
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>表路由键</returns>
    protected virtual string GetTableRouteKey(DatabaseContext databaseContext) => databaseContext?.TenantId ?? string.Empty;

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
    /// <returns>实体映射元数据</returns>
    protected virtual EntityMappingMetadata CreateMapping(Type entityType, DatabaseContext databaseContext, string schema,
        string tableName)
    {
        var descriptor = GetDescriptor(entityType);
        var columns = descriptor.Properties.ToDictionary(
            t => t.Name,
            t => CreateColumnMetadata(entityType, t),
            StringComparer.OrdinalIgnoreCase);
        return new EntityMappingMetadata
        {
            EntityType = entityType,
            DbKey = databaseContext.DbKey,
            DatabaseType = databaseContext.DatabaseType,
            Role = databaseContext.Role,
            Schema = schema,
            TableName = tableName,
            FullTableName = string.IsNullOrWhiteSpace(schema) ? tableName : $"{schema}.{tableName}",
            TableRouteKey = GetTableRouteKey(databaseContext),
            MappingVersion = databaseContext.MappingVersion,
            Columns = columns
        };
    }

    /// <summary>
    /// 创建列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="property">属性信息</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata CreateColumnMetadata(Type entityType, PropertyInfo property)
    {
        var propertyType = GetUnderlyingType(property.PropertyType);
        var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
        var providerTypeName = columnAttribute?.TypeName;
        var storageKind = GetStorageKind(propertyType, providerTypeName);
        var column = new ColumnMappingMetadata
        {
            PropertyName = property.Name,
            ColumnName = _entityMetadata.GetColumn(entityType, property.Name) ?? property.Name,
            ClrType = property.PropertyType,
            DbType = GetDbType(propertyType, providerTypeName, GetSize(property)),
            Size = GetSize(property),
            Precision = GetPrecision(providerTypeName),
            Scale = GetScale(providerTypeName),
            ProviderTypeName = providerTypeName,
            IsNullable = IsNullable(property.PropertyType),
            StorageKind = storageKind,
            ConverterKind = GetConverterKind(propertyType, storageKind),
            CustomConverterName = null
        };
        return column;
    }

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
    /// <returns>DbType</returns>
    protected virtual DbType? GetDbType(Type type, string providerTypeName, int? length)
    {
        var dbType = _typeConverter?.ToDbType(providerTypeName, length);
        if (dbType != null)
            return dbType;
        if (type.IsEnum)
            return GetDbType(Enum.GetUnderlyingType(type), providerTypeName, length);
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
