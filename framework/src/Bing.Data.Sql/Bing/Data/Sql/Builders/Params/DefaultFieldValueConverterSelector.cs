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
    /// 初始化一个<see cref="DefaultFieldValueConverterSelector"/>类型的实例
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

    /// <summary>
    /// 将值转换为 Provider 可识别的参数值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>Provider 参数值</returns>
    public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext)
    {
        var converter = GetConverter(column, databaseContext);
        return converter == null ? value : converter.ConvertToProvider(value, column, databaseContext);
    }

    /// <summary>
    /// 将 Provider 返回值转换为 CLR 值
    /// </summary>
    /// <param name="value">Provider 返回值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>CLR 值</returns>
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
    protected virtual IFieldValueConverter GetConverter(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        _converters.FirstOrDefault(t => t.CanConvert(column, databaseContext));
}
