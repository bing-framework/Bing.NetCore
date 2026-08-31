namespace Bing.Data.Sql;

/// <summary>
/// 指定字段值在参数写入和结果读取阶段采用的转换策略。
/// </summary>
public enum FieldValueConverterKind
{
    /// <summary>
    /// 保持字段值不变。
    /// </summary>
    None = 0,

    /// <summary>
    /// 将枚举值写入为枚举名称，并在读取时解析为枚举值。
    /// </summary>
    EnumToName = 1,

    /// <summary>
    /// 将枚举值写入为其基础数值，并在读取时还原为枚举值。
    /// </summary>
    EnumToValue = 2,

    /// <summary>
    /// 将布尔值写入为数值，并在读取时将非零值视为 <c>true</c>。
    /// </summary>
    BoolToNumber = 3,

    /// <summary>
    /// 将布尔值写入为配置的字符串值，并在读取时按配置的真值字符串判断。
    /// </summary>
    BoolToString = 4,

    /// <summary>
    /// 将 <see cref="Guid"/> 写入为字符串，并在读取时尝试解析为 <see cref="Guid"/>。
    /// </summary>
    GuidToString = 5,

    /// <summary>
    /// 将 <see cref="Guid"/> 写入为二进制值，并在读取到 16 字节数组时还原为 <see cref="Guid"/>。
    /// </summary>
    GuidToBinary = 6,

    /// <summary>
    /// 在写入参数前将值序列化为 JSON 文本；默认读取转换不会自动反序列化。
    /// </summary>
    JsonSerialize = 7,

    /// <summary>
    /// 在写入参数前按列数据库类型规范化日期时间值；默认读取转换不会执行反向转换。
    /// </summary>
    DateTimeNormalize = 8,

    /// <summary>
    /// 使用由映射配置指定的自定义转换器。
    /// </summary>
    Custom = 99
}
