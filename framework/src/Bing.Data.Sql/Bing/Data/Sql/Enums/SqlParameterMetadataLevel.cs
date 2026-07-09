namespace Bing.Data.Sql;

/// <summary>
/// Sql参数元数据等级
/// </summary>
public enum SqlParameterMetadataLevel
{
    /// <summary>
    /// 无元数据
    /// </summary>
    None = 0,

    /// <summary>
    /// 弱元数据
    /// </summary>
    Weak = 1,

    /// <summary>
    /// 部分元数据
    /// </summary>
    Partial = 2,

    /// <summary>
    /// 完整元数据
    /// </summary>
    Full = 3
}
