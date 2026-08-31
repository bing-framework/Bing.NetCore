namespace Bing.Trees;

/// <summary>
/// 指定树形数据加载节点时采用的同步或异步策略。
/// </summary>
public enum LoadMode
{
    /// <summary>
    /// 使用同步方式加载节点。
    /// </summary>
    Sync,

    /// <summary>
    /// 使用异步方式加载节点。
    /// </summary>
    Async,

    /// <summary>
    /// 仅异步加载根节点，下级节点在后续一次性加载。
    /// </summary>
    OnlyRootAsync
}
