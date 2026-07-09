namespace Bing.Data.Sql;

/// <summary>
/// 表路由类型
/// </summary>
public enum TableRouteKind
{
    /// <summary>
    /// 无路由
    /// </summary>
    None = 0,

    /// <summary>
    /// 租户路由
    /// </summary>
    Tenant = 1,

    /// <summary>
    /// 按年路由
    /// </summary>
    Year = 2,

    /// <summary>
    /// 按年月路由
    /// </summary>
    YearMonth = 3,

    /// <summary>
    /// 按日期路由
    /// </summary>
    Date = 4,

    /// <summary>
    /// 按哈希路由
    /// </summary>
    Hash = 5,

    /// <summary>
    /// 自定义路由
    /// </summary>
    Custom = 99
}
