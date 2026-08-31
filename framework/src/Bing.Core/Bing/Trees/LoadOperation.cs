namespace Bing.Trees;

/// <summary>
/// 标识树形数据加载请求的触发场景。
/// </summary>
public enum LoadOperation
{
    /// <summary>
    /// 首次加载树形数据。
    /// </summary>
    FirstLoad = 1,

    /// <summary>
    /// 加载指定节点的子节点。
    /// </summary>
    LoadChild = 2,

    /// <summary>
    /// 根据搜索条件加载匹配的树形数据。
    /// </summary>
    Search = 3
}
