namespace Bing.Data.Sql;

/// <summary>
/// 类型化 Join 的高级来源选项。
/// </summary>
public struct SqlJoinOptions
{
    /// <summary>
    /// 右侧来源别名。
    /// </summary>
    public string RightAlias { get; init; }

    /// <summary>
    /// 左侧来源别名；未设置时按当前来源解析。
    /// </summary>
    public string LeftAlias { get; init; }

    /// <summary>
    /// 右侧来源架构名。
    /// </summary>
    public string Schema { get; init; }
}
