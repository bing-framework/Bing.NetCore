using System.Data;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Extensions;
using Bing.Utils.Json;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 默认字段值转换器
/// </summary>
public class DefaultFieldValueConverter : IFieldValueConverter
{
    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultFieldValueConverter"/>类型的实例
    /// </summary>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultFieldValueConverter(SqlMetadataOptions options = null) => _options = options ?? new SqlMetadataOptions();

    /// <summary>
    /// 是否支持当前列映射转换
    /// </summary>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>是否支持转换</returns>
    public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) => column != null;

    /// <summary>
    /// 将值转换为 Provider 可识别的参数值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>Provider 参数值</returns>
    public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext)
    {
        if (value == null || column == null)
            return value;
        switch (column.ConverterKind)
        {
            case FieldValueConverterKind.EnumToName:
                return ConvertEnumToName(value, column);
            case FieldValueConverterKind.EnumToValue:
                return ConvertEnumToValue(value, column);
            case FieldValueConverterKind.BoolToNumber:
                return ConvertBoolToNumber(value);
            case FieldValueConverterKind.BoolToString:
                return ConvertBoolToString(value);
            case FieldValueConverterKind.GuidToString:
                return ConvertGuidToString(value);
            case FieldValueConverterKind.GuidToBinary:
                return ConvertGuidToBinary(value);
            case FieldValueConverterKind.JsonSerialize:
                return JsonHelper.ToJson(value);
            case FieldValueConverterKind.DateTimeNormalize:
                return ConvertDateTime(value, column.DbType);
            default:
                return value;
        }
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
        if (value == null || column == null)
            return value;
        var targetType = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        switch (column.ConverterKind)
        {
            case FieldValueConverterKind.EnumToName:
                return targetType.IsEnum ? Enum.Parse(targetType, value.SafeString(), true) : value;
            case FieldValueConverterKind.EnumToValue:
                return targetType.IsEnum ? Enum.ToObject(targetType, value) : value;
            case FieldValueConverterKind.BoolToNumber:
                return Convert.ToInt32(value) != 0;
            case FieldValueConverterKind.BoolToString:
                return string.Equals(value.SafeString(), _options.BoolTrueValue, StringComparison.OrdinalIgnoreCase);
            case FieldValueConverterKind.GuidToString:
                return Guid.TryParse(value.SafeString(), out var guidValue) ? guidValue : value;
            case FieldValueConverterKind.GuidToBinary:
                return value is byte[] bytes && bytes.Length == 16 ? new Guid(bytes) : value;
            default:
                return value;
        }
    }

    /// <summary>
    /// 枚举转名称
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="column">列映射元数据</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertEnumToName(object value, ColumnMappingMetadata column)
    {
        var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        if (type.IsEnum == false)
            return value;
        if (value.GetType().IsEnum)
            return value.ToString();
        return Enum.GetName(type, value) ?? value;
    }

    /// <summary>
    /// 枚举转值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="column">列映射元数据</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertEnumToValue(object value, ColumnMappingMetadata column)
    {
        var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        if (type.IsEnum == false)
            return value;
        if (value.GetType().IsEnum)
            return Convert.ChangeType(value, Enum.GetUnderlyingType(type));
        return value;
    }

    /// <summary>
    /// 布尔转数字
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertBoolToNumber(object value) => Convert.ToBoolean(value) ? 1 : 0;

    /// <summary>
    /// 布尔转字符串
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertBoolToString(object value) => Convert.ToBoolean(value) ? _options.BoolTrueValue : _options.BoolFalseValue;

    /// <summary>
    /// Guid 转字符串
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertGuidToString(object value) => value is Guid guidValue ? guidValue.ToString("D") : value;

    /// <summary>
    /// Guid 转二进制
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertGuidToBinary(object value) => value is Guid guidValue ? guidValue.ToByteArray() : value;

    /// <summary>
    /// 标准化日期时间值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="dbType">数据库参数类型</param>
    /// <returns>转换后的值</returns>
    protected virtual object ConvertDateTime(object value, DbType? dbType)
    {
        if (value is DateTimeOffset offset)
        {
            return dbType == DbType.DateTimeOffset ? value : offset.UtcDateTime;
        }
        if (value is DateTime dateTime)
            return dateTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dateTime, DateTimeKind.Local) : dateTime;
        return value;
    }
}
