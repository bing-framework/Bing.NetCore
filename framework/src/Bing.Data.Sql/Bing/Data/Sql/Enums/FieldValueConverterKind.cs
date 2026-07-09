namespace Bing.Data.Sql;

/// <summary>
/// 字段值转换器类型
/// </summary>
public enum FieldValueConverterKind
{
    /// <summary>
    /// 不转换
    /// </summary>
    None = 0,

    /// <summary>
    /// 枚举转名称
    /// </summary>
    EnumToName = 1,

    /// <summary>
    /// 枚举转值
    /// </summary>
    EnumToValue = 2,

    /// <summary>
    /// 布尔转数字
    /// </summary>
    BoolToNumber = 3,

    /// <summary>
    /// 布尔转字符串
    /// </summary>
    BoolToString = 4,

    /// <summary>
    /// Guid 转字符串
    /// </summary>
    GuidToString = 5,

    /// <summary>
    /// Guid 转二进制
    /// </summary>
    GuidToBinary = 6,

    /// <summary>
    /// Json 序列化
    /// </summary>
    JsonSerialize = 7,

    /// <summary>
    /// 日期时间标准化
    /// </summary>
    DateTimeNormalize = 8,

    /// <summary>
    /// 自定义转换器
    /// </summary>
    Custom = 99
}
