using System.Collections;
using System.Data;
using System.Reflection;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

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