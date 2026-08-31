namespace Bing.Data.Sql;

/// <summary>
/// 指定实体属性在数据库列中的存储表示方式。
/// </summary>
public enum ColumnStorageKind
{
    /// <summary>
    /// 由属性类型或 Provider 选择默认存储方式。
    /// </summary>
    Default = 0,

    /// <summary>
    /// 按字符串值存储。
    /// </summary>
    String = 1,

    /// <summary>
    /// 按数值类型存储。
    /// </summary>
    Number = 2,

    /// <summary>
    /// 按布尔类型存储。
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// 按 GUID 类型存储。
    /// </summary>
    Guid = 4,

    /// <summary>
    /// 按二进制数据存储。
    /// </summary>
    Binary = 5,

    /// <summary>
    /// 按 JSON 文本或 Provider 支持的 JSON 类型存储。
    /// </summary>
    Json = 6,

    /// <summary>
    /// 将枚举名称作为字符串存储。
    /// </summary>
    EnumName = 7,

    /// <summary>
    /// 将枚举底层数值作为数值存储。
    /// </summary>
    EnumValue = 8,

    /// <summary>
    /// 按不带时区的日期时间存储。
    /// </summary>
    DateTime = 9,

    /// <summary>
    /// 按带偏移量的日期时间存储。
    /// </summary>
    DateTimeOffset = 10
}
