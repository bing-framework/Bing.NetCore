namespace Bing.Trees;

/// <summary>
/// 定义树节点物化路径及其层级信息的契约。
/// </summary>
public interface IPath
{
    /// <summary>
    /// 获取节点从根节点到当前节点的物化路径。
    /// </summary>
    string Path { get; }

    /// <summary>
    /// 获取节点在树中的层级，根节点通常为第 1 级。
    /// </summary>
    int Level { get; }
}