using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 字段值转换器选择器
/// </summary>
public interface IFieldValueConverterSelector
{
    /// <summary>
    /// 将值转换为 Provider 可识别的参数值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>Provider 参数值</returns>
    object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext);

    /// <summary>
    /// 将 Provider 返回值转换为 CLR 值
    /// </summary>
    /// <param name="value">Provider 返回值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>CLR 值</returns>
    object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext);
}
