namespace Bing.Data.Sql;

/// <summary>
/// 指定 SQL 查询选择主库或由数据源决定读取位置的偏好。
/// </summary>
public enum SqlReadPreference
{
    /// <summary>
    /// 使用数据源或 Provider 的默认读取策略。
    /// </summary>
    Default,

    /// <summary>
    /// 优先从主库读取，以获得最新提交的数据。
    /// </summary>
    Primary
}