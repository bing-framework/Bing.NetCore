namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 聚合函数。
/// </summary>
public enum SqlAggregateFunction
{
    /// <summary>
    /// 计数。
    /// </summary>
    Count,

    /// <summary>
    /// 求和。
    /// </summary>
    Sum,

    /// <summary>
    /// 求平均值。
    /// </summary>
    Avg,

    /// <summary>
    /// 求最大值。
    /// </summary>
    Max,

    /// <summary>
    /// 求最小值。
    /// </summary>
    Min
}