namespace Bing.Data.Sql;

/// <summary>
/// 指定实体物理表名根据何种维度进行路由。
/// </summary>
public enum TableRouteKind
{
    /// <summary>
    /// 不进行动态表路由，直接使用基础表名。
    /// </summary>
    None = 0,

    /// <summary>
    /// 根据租户标识选择物理表。
    /// </summary>
    Tenant = 1,

    /// <summary>
    /// 根据年份选择物理表。
    /// </summary>
    Year = 2,

    /// <summary>
    /// 根据年份和月份选择物理表。
    /// </summary>
    YearMonth = 3,

    /// <summary>
    /// 根据日期选择物理表。
    /// </summary>
    Date = 4,

    /// <summary>
    /// 根据路由值的哈希结果选择物理表。
    /// </summary>
    Hash = 5,

    /// <summary>
    /// 使用调用方或扩展点定义的路由规则选择物理表。
    /// </summary>
    Custom = 99
}
