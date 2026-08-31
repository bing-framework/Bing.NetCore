namespace Bing.Data.Sql;

/// <summary>
/// 表示 SQL 参数绑定时可用的元数据完整程度。
/// </summary>
public enum SqlParameterMetadataLevel
{
    /// <summary>
    /// 没有可用的参数来源或类型等元数据。
    /// </summary>
    None = 0,

    /// <summary>
    /// 仅有不足以完整描述参数的弱元数据。
    /// </summary>
    Weak = 1,

    /// <summary>
    /// 已解析部分参数元数据，但仍缺少完整映射信息。
    /// </summary>
    Partial = 2,

    /// <summary>
    /// 已获得可用于完整绑定和诊断的参数元数据。
    /// </summary>
    Full = 3
}
