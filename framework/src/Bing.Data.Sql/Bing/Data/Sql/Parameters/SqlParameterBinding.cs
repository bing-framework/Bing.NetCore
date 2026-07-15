using System.Collections;
using System.Data;
using System.Reflection;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数名称规范化器
/// </summary>
public interface ISqlParameterNameNormalizer
{
    /// <summary>
    /// 将参数名称转换为不含 Provider 前缀的标准名称
    /// </summary>
    /// <param name="name">原始参数名称</param>
    /// <returns>标准参数名称</returns>
    string Normalize(string name);
}

/// <summary>
/// 默认 SQL 参数名称规范化器
/// </summary>
public sealed class DefaultSqlParameterNameNormalizer : ISqlParameterNameNormalizer
{
    /// <inheritdoc />
    public string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }
}

/// <summary>
/// SQL 参数源解析器
/// </summary>
public interface ISqlParameterSourceResolver
{
    /// <summary>
    /// 判断是否支持参数源
    /// </summary>
    /// <param name="source">参数源</param>
    /// <returns>支持时返回 true</returns>
    bool CanResolve(object source);

    /// <summary>
    /// 尝试解析参数值
    /// </summary>
    /// <param name="source">参数源</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    bool TryResolve(object source, string parameterName, out object value);
}

/// <summary>
/// 默认 SQL 参数源解析器
/// </summary>
public sealed class DefaultSqlParameterSourceResolver : ISqlParameterSourceResolver
{
    /// <summary>
    /// 参数名称规范化器
    /// </summary>
    private readonly ISqlParameterNameNormalizer _nameNormalizer;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterSourceResolver"/>类型的实例
    /// </summary>
    /// <param name="nameNormalizer">参数名称规范化器</param>
    public DefaultSqlParameterSourceResolver(ISqlParameterNameNormalizer nameNormalizer = null) =>
        _nameNormalizer = nameNormalizer ?? new DefaultSqlParameterNameNormalizer();

    /// <inheritdoc />
    public bool CanResolve(object source) => source != null && source is not ISqlParameterMap;

    /// <inheritdoc />
    public bool TryResolve(object source, string parameterName, out object value)
    {
        value = null;
        if (CanResolve(source) == false || string.IsNullOrWhiteSpace(parameterName))
            return false;
        var normalizedName = _nameNormalizer.Normalize(parameterName);
        if (source is IEnumerable<SqlParam> sqlParameters)
        {
            var parameter = sqlParameters.FirstOrDefault(t => t != null &&
                string.Equals(_nameNormalizer.Normalize(t.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
            if (parameter == null)
                return false;
            value = parameter.Value;
            return true;
        }
        if (source is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return TryResolve(readOnlyDictionary, normalizedName, out value);
        if (source is IDictionary<string, object> dictionary)
            return TryResolve(dictionary, normalizedName, out value);
        if (source is IDictionary nonGenericDictionary)
            return TryResolve(nonGenericDictionary, normalizedName, out value);
        var property = source.GetType().GetProperty(normalizedName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property == null || property.CanRead == false || property.GetIndexParameters().Length != 0)
            return false;
        value = property.GetValue(source);
        return true;
    }

    /// <summary>
    /// 从泛型字典解析参数
    /// </summary>
    /// <param name="source">参数字典</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    private bool TryResolve(IEnumerable<KeyValuePair<string, object>> source, string parameterName, out object value)
    {
        foreach (var item in source)
        {
            if (string.Equals(_nameNormalizer.Normalize(item.Key), parameterName, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            value = item.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// 从非泛型字典解析参数
    /// </summary>
    /// <param name="source">参数字典</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    private bool TryResolve(IDictionary source, string parameterName, out object value)
    {
        foreach (DictionaryEntry item in source)
        {
            if (item.Key is not string name ||
                string.Equals(_nameNormalizer.Normalize(name), parameterName, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            value = item.Value;
            return true;
        }
        value = null;
        return false;
    }
}

/// <summary>
/// SQL 参数绑定上下文
/// </summary>
public sealed class SqlParameterBindingContext
{
    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 原始参数源
    /// </summary>
    public object Source { get; set; }
}

/// <summary>
/// SQL 参数绑定项
/// </summary>
public sealed class SqlParameterBindingItem
{
    /// <summary>
    /// 标准参数名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 原始参数值
    /// </summary>
    public object OriginalValue { get; set; }

    /// <summary>
    /// 最终参数值
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 是否存在参数值
    /// </summary>
    public bool HasValue { get; set; }

    /// <summary>
    /// 是否显式空值
    /// </summary>
    public bool IsExplicitNull { get; set; }

    /// <summary>
    /// 参数元数据
    /// </summary>
    public SqlParam Metadata { get; set; }
}

/// <summary>
/// SQL 参数绑定结果
/// </summary>
public sealed class SqlParameterBindingResult
{
    /// <summary>
    /// 参数绑定项
    /// </summary>
    public IReadOnlyList<SqlParameterBindingItem> Items { get; set; } = Array.Empty<SqlParameterBindingItem>();

    /// <summary>
    /// 原始参数类型名称
    /// </summary>
    public string OriginalParameterType { get; set; }

    /// <summary>
    /// 是否使用元数据绑定
    /// </summary>
    public bool IsMetadataBound { get; set; }
}

/// <summary>
/// SQL 参数绑定解析器
/// </summary>
public interface ISqlParameterResolver
{
    /// <summary>
    /// 解析参数绑定结果
    /// </summary>
    /// <param name="context">参数绑定上下文</param>
    /// <returns>参数绑定结果</returns>
    SqlParameterBindingResult Resolve(SqlParameterBindingContext context);
}

/// <summary>
/// 默认 SQL 参数绑定解析器
/// </summary>
public sealed class DefaultSqlParameterResolver : ISqlParameterResolver
{
    /// <summary>
    /// 参数名称规范化器
    /// </summary>
    private readonly ISqlParameterNameNormalizer _nameNormalizer;

    /// <summary>
    /// 参数源解析器
    /// </summary>
    private readonly ISqlParameterSourceResolver _sourceResolver;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterResolver"/>类型的实例
    /// </summary>
    /// <param name="nameNormalizer">参数名称规范化器</param>
    /// <param name="sourceResolver">参数源解析器</param>
    public DefaultSqlParameterResolver(ISqlParameterNameNormalizer nameNormalizer = null,
        ISqlParameterSourceResolver sourceResolver = null)
    {
        _nameNormalizer = nameNormalizer ?? new DefaultSqlParameterNameNormalizer();
        _sourceResolver = sourceResolver ?? new DefaultSqlParameterSourceResolver(_nameNormalizer);
    }

    /// <inheritdoc />
    public SqlParameterBindingResult Resolve(SqlParameterBindingContext context)
    {
        context ??= new SqlParameterBindingContext();
        var map = context.Source as ISqlParameterMap;
        var source = map != null ? map.Source : context.Source;
        var items = ExtractItems(source).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        if (map != null)
            ApplyMap(items, map, source, context);
        return new SqlParameterBindingResult
        {
            Items = items.Values.ToList(),
            OriginalParameterType = context.Source?.GetType().FullName,
            IsMetadataBound = map != null || items.Values.Any(t => t.Metadata != null)
        };
    }

    /// <summary>
    /// 提取原始参数项
    /// </summary>
    /// <param name="source">参数源</param>
    /// <returns>参数绑定项集合</returns>
    private IEnumerable<SqlParameterBindingItem> ExtractItems(object source)
    {
        if (source == null)
            return Array.Empty<SqlParameterBindingItem>();
        if (source is IEnumerable<SqlParam> sqlParameters)
            return sqlParameters.Where(t => t != null).Select(CreateItem);
        if (source is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.Select(t => CreateItem(t.Key, t.Value));
        if (source is IDictionary<string, object> dictionary)
            return dictionary.Select(t => CreateItem(t.Key, t.Value));
        if (source is IDictionary nonGenericDictionary)
            return nonGenericDictionary.Cast<DictionaryEntry>()
                .Where(t => t.Key is string)
                .Select(t => CreateItem((string)t.Key, t.Value));
        return source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(t => t.CanRead && t.GetIndexParameters().Length == 0)
            .Select(t => CreateItem(t.Name, t.GetValue(source)));
    }

    /// <summary>
    /// 创建原始参数绑定项
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>参数绑定项</returns>
    private SqlParameterBindingItem CreateItem(string name, object value)
    {
        return new SqlParameterBindingItem
        {
            Name = _nameNormalizer.Normalize(name),
            OriginalValue = value,
            Value = value,
            HasValue = true,
            IsExplicitNull = value == null
        };
    }

    /// <summary>
    /// 通过增强参数创建绑定项
    /// </summary>
    /// <param name="parameter">增强参数</param>
    /// <returns>参数绑定项</returns>
    private SqlParameterBindingItem CreateItem(SqlParam parameter)
    {
        return new SqlParameterBindingItem
        {
            Name = _nameNormalizer.Normalize(parameter.Name),
            OriginalValue = parameter.OriginalValue ?? parameter.Value,
            Value = parameter.Value,
            HasValue = true,
            IsExplicitNull = parameter.Value == null,
            Metadata = parameter
        };
    }

    /// <summary>
    /// 应用参数映射
    /// </summary>
    /// <param name="items">原始参数项</param>
    /// <param name="map">参数映射</param>
    /// <param name="source">参数源</param>
    /// <param name="context">参数绑定上下文</param>
    private void ApplyMap(IDictionary<string, SqlParameterBindingItem> items, ISqlParameterMap map, object source,
        SqlParameterBindingContext context)
    {
        foreach (var mapItem in map.GetItems())
        {
            if (mapItem == null || string.IsNullOrWhiteSpace(mapItem.Name))
                continue;
            var name = _nameNormalizer.Normalize(mapItem.Name);
            items.TryGetValue(name, out var originalItem);
            object value = null;
            var hasValue = mapItem.HasExplicitValue || mapItem.ValueResolved ||
                           _sourceResolver.TryResolve(source, name, out value) ||
                           _sourceResolver.TryResolve(source, mapItem.PropertyName, out value);
            var finalValue = mapItem.HasExplicitValue || mapItem.ValueResolved ? mapItem.Value : value;
            if (hasValue == false && RequiresInput(mapItem))
                throw new SqlParameterBindingException(name, context, mapItem.PropertyName);
            var metadata = CreateMetadata(name, finalValue, mapItem, originalItem?.Metadata);
            items[name] = new SqlParameterBindingItem
            {
                Name = name,
                OriginalValue = originalItem?.OriginalValue ?? finalValue,
                Value = finalValue,
                HasValue = hasValue,
                IsExplicitNull = hasValue && finalValue == null,
                Metadata = metadata
            };
        }
    }

    /// <summary>
    /// 判断映射项是否需要输入值
    /// </summary>
    /// <param name="item">参数映射项</param>
    /// <returns>需要输入值时返回 true</returns>
    private static bool RequiresInput(SqlParameterMapItem item)
    {
        return item.Direction is not ParameterDirection.Output and not ParameterDirection.ReturnValue;
    }

    /// <summary>
    /// 创建映射后的参数元数据
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">参数值</param>
    /// <param name="mapItem">参数映射项</param>
    /// <param name="originalMetadata">原始参数元数据</param>
    /// <returns>增强参数元数据</returns>
    private static SqlParam CreateMetadata(string name, object value, SqlParameterMapItem mapItem,
        SqlParam originalMetadata)
    {
        return new SqlParam(name, value, mapItem.DbType ?? originalMetadata?.DbType,
            mapItem.Direction ?? originalMetadata?.Direction, mapItem.Size ?? originalMetadata?.Size,
            mapItem.Precision ?? originalMetadata?.Precision, mapItem.Scale ?? originalMetadata?.Scale)
        {
            OriginalValue = originalMetadata?.OriginalValue ?? originalMetadata?.Value ?? value,
            EntityType = mapItem.EntityType,
            PropertyName = mapItem.PropertyName,
            ColumnName = originalMetadata?.ColumnName,
            DatabaseType = originalMetadata?.DatabaseType,
            ProviderTypeName = originalMetadata?.ProviderTypeName,
            Source = SqlParameterSource.RawSql,
            MetadataLevel = originalMetadata?.MetadataLevel ?? SqlParameterMetadataLevel.Weak,
            StorageKind = originalMetadata?.StorageKind ?? ColumnStorageKind.Default,
            ConverterKind = originalMetadata?.ConverterKind ?? FieldValueConverterKind.None,
            CustomConverterName = originalMetadata?.CustomConverterName
        };
    }
}

/// <summary>
/// SQL 参数绑定异常
/// </summary>
public sealed class SqlParameterBindingException : Exception
{
    /// <summary>
    /// 初始化一个<see cref="SqlParameterBindingException"/>类型的实例
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="context">参数绑定上下文</param>
    /// <param name="propertyName">关联属性名称</param>
    public SqlParameterBindingException(string parameterName, SqlParameterBindingContext context,
        string propertyName = null)
        : base(CreateMessage(parameterName, context, propertyName))
    {
        ParameterName = parameterName;
        Sql = context?.Sql;
        DbKey = context?.DbKey;
        SourceType = context?.Source?.GetType();
        EntityType = context?.EntityType;
        PropertyName = propertyName;
    }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; }

    /// <summary>
    /// 参数源类型
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 关联属性名称
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 创建异常消息
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="context">参数绑定上下文</param>
    /// <param name="propertyName">关联属性名称</param>
    /// <returns>异常消息</returns>
    private static string CreateMessage(string parameterName, SqlParameterBindingContext context, string propertyName)
    {
        return $"无法解析 SQL 参数 '{parameterName}'。SQL: {context?.Sql ?? "<未提供>"}；DbKey: {context?.DbKey ?? "<未提供>"}；" +
               $"参数源类型: {context?.Source?.GetType().FullName ?? "<未提供>"}；实体类型: {context?.EntityType?.FullName ?? "<未提供>"}；" +
               $"关联属性: {propertyName ?? "<未提供>"}。";
    }
}

/// <summary>
/// SQL 输出参数访问器
/// </summary>
public interface ISqlOutputParameterAccessor
{
    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <returns>输出参数值</returns>
    object GetValue(string name);

    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <returns>转换后的输出参数值</returns>
    T GetValue<T>(string name);

    /// <summary>
    /// 尝试获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <param name="value">输出参数值</param>
    /// <returns>获取成功时返回 true</returns>
    bool TryGetValue<T>(string name, out T value);
}

/// <summary>
/// SQL 数据库参数定制器
/// </summary>
public interface ISqlDbParameterCustomizer
{
    /// <summary>
    /// 判断是否支持数据库类型
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>支持时返回 true</returns>
    bool CanHandle(DatabaseType databaseType);

    /// <summary>
    /// 配置数据库参数
    /// </summary>
    /// <param name="dbParameter">数据库参数</param>
    /// <param name="sqlParameter">SQL 参数元数据</param>
    void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter);
}