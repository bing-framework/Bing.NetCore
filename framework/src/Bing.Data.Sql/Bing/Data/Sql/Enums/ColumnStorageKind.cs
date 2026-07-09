namespace Bing.Data.Sql;

/// <summary>
/// 字段存储方式
/// </summary>
public enum ColumnStorageKind
{
    /// <summary>
    /// 默认存储方式
    /// </summary>
    Default = 0,

    /// <summary>
    /// 字符串
    /// </summary>
    String = 1,

    /// <summary>
    /// 数值
    /// </summary>
    Number = 2,

    /// <summary>
    /// 布尔
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// Guid
    /// </summary>
    Guid = 4,

    /// <summary>
    /// 二进制
    /// </summary>
    Binary = 5,

    /// <summary>
    /// Json
    /// </summary>
    Json = 6,

    /// <summary>
    /// 枚举名称
    /// </summary>
    EnumName = 7,

    /// <summary>
    /// 枚举值
    /// </summary>
    EnumValue = 8,

    /// <summary>
    /// 日期时间
    /// </summary>
    DateTime = 9,

    /// <summary>
    /// 带时区日期时间
    /// </summary>
    DateTimeOffset = 10
}
