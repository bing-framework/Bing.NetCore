namespace Bing.Data.Queries;

/// <summary>
/// 指定范围查询的上下界是否包含在结果范围内。
/// </summary>
public enum Boundary
{
    /// <summary>
    /// 包含最小值，不包含最大值。
    /// </summary>
    Left,

    /// <summary>
    /// 不包含最小值，包含最大值。
    /// </summary>
    Right,

    /// <summary>
    /// 同时包含最小值和最大值。
    /// </summary>
    Both,

    /// <summary>
    /// 同时不包含最小值和最大值。
    /// </summary>
    Neither
}