namespace Bing.Data.Sql;

/// <summary>
/// SQL 读取偏好
/// </summary>
public enum SqlReadPreference
{
    /// <summary>
    /// 默认策略
    /// </summary>
    Default,

    /// <summary>
    /// 优先主库
    /// </summary>
    Primary
}