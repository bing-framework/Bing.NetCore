namespace Bing.Data.Sql;

/// <summary>
/// 配置类型化 Join 的来源别名和右侧架构。
/// </summary>
public struct SqlJoinOptions
{
    /// <summary>
    /// 获取或初始化右侧来源别名。
    /// </summary>
    public string RightAlias { get; init; }

    /// <summary>
    /// 获取或初始化左侧来源别名。
    /// </summary>
    /// <remarks>
    /// 未设置时按当前来源解析。
    /// </remarks>
    public string LeftAlias { get; init; }

    /// <summary>
    /// 获取或初始化右侧来源架构名。
    /// </summary>
    public string Schema { get; init; }
}
