using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 默认字段值转换器选择器
/// </summary>
public class DefaultFieldValueConverterSelector : IFieldValueConverterSelector
{
    /// <summary>
    /// 字段值转换器集合
    /// </summary>
    private readonly IReadOnlyList<IFieldValueConverter> _converters;

    /// <summary>
    /// 获取是否仅使用内置默认转换管线，不包含派生选择器或自定义转换器。
    /// </summary>
    /// <remarks>执行器仅可对转换类型为 None 的列采用等价读取策略；其他转换仍须按列执行。</remarks>
    public bool UsesDefaultConverter => GetType() == typeof(DefaultFieldValueConverterSelector) &&
        _converters.Count == 1 && _converters[0].GetType() == typeof(DefaultFieldValueConverter);

    /// <summary>
    /// 初始化一个 <see cref="DefaultFieldValueConverterSelector"/> 类型的实例。
    /// </summary>
    /// <param name="converters">字段值转换器集合</param>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultFieldValueConverterSelector(IEnumerable<IFieldValueConverter> converters = null,
        SqlMetadataOptions options = null)
    {
        var items = converters?.ToList() ?? new List<IFieldValueConverter>();
        if (items.Count == 0)
            items.Add(new DefaultFieldValueConverter(options));
        _converters = items;
    }

    /// <inheritdoc />
    public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext)
    {
        var converter = GetConverter(column, databaseContext);
        return converter == null ? value : converter.ConvertToProvider(value, column, databaseContext);
    }

    /// <inheritdoc />
    public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext)
    {
        var converter = GetConverter(column, databaseContext);
        return converter == null ? value : converter.ConvertFromProvider(value, column, databaseContext);
    }

    /// <summary>
    /// 获取字段值转换器
    /// </summary>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>字段值转换器</returns>
    protected virtual IFieldValueConverter GetConverter(ColumnMappingMetadata column, DatabaseContext databaseContext)
    {
        // 每个字段都会经过选择器；避免在逐行读取时为谓词捕获列和上下文创建闭包。
        for (var index = 0; index < _converters.Count; index++)
            if (_converters[index].CanConvert(column, databaseContext))
                return _converters[index];
        return null;
    }
}
