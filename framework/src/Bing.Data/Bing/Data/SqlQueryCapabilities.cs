namespace Bing.Data;

/// <summary>
/// SQL 查询能力状态。
/// </summary>
public enum SqlQueryCapabilityState
{
    /// <summary>
    /// 不覆盖 Provider 或数据源的能力声明。
    /// </summary>
    Inherit = 0,

    /// <summary>
    /// 不支持该查询语法。
    /// </summary>
    Unsupported = 1,

    /// <summary>
    /// 已确认支持该查询语法。
    /// </summary>
    Supported = 2
}

/// <summary>
/// SQL 查询语法能力配置。
/// </summary>
/// <remarks>
/// 值为 <see cref="SqlQueryCapabilityState.Inherit"/> 时不覆盖 Provider 或数据源的能力声明。
/// 对受数据库版本影响的语法，应仅在已验证目标数据源后设置为 <see cref="SqlQueryCapabilityState.Supported"/>。
/// </remarks>
public sealed class SqlQueryCapabilities
{
    /// <summary>
    /// 获取或设置公用表表达式（CTE）能力状态。
    /// </summary>
    public SqlQueryCapabilityState Cte { get; set; }

    /// <summary>
    /// 获取或设置 <c>UNION</c> 集合运算能力状态。
    /// </summary>
    public SqlQueryCapabilityState Union { get; set; }

    /// <summary>
    /// 获取或设置 <c>UNION ALL</c> 集合运算能力状态。
    /// </summary>
    public SqlQueryCapabilityState UnionAll { get; set; }

    /// <summary>
    /// 获取或设置 <c>INTERSECT</c> 集合运算能力状态。
    /// </summary>
    public SqlQueryCapabilityState Intersect { get; set; }

    /// <summary>
    /// 获取或设置 <c>EXCEPT</c> 集合运算能力状态。
    /// </summary>
    public SqlQueryCapabilityState Except { get; set; }

    /// <summary>
    /// 获取或设置 <c>RIGHT JOIN</c> 连接能力状态。
    /// </summary>
    public SqlQueryCapabilityState RightJoin { get; set; }

    /// <summary>
    /// 获取或设置 <c>FULL JOIN</c> 连接能力状态。
    /// </summary>
    public SqlQueryCapabilityState FullJoin { get; set; }

    /// <summary>
    /// 获取或设置 <c>Skip</c>、<c>Take</c> 和 <c>Page</c> 分页能力状态。
    /// </summary>
    public SqlQueryCapabilityState Pagination { get; set; }
}
