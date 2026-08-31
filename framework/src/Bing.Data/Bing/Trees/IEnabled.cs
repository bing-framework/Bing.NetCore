namespace Bing.Trees;

/// <summary>
/// 定义树节点启用状态的契约。
/// </summary>
public interface IEnabled
{
    /// <summary>
    /// 获取或设置节点是否启用。
    /// </summary>
    bool Enabled { get; set; }
}