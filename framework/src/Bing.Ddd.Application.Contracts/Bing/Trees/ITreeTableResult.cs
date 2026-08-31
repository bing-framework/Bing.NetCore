// ReSharper disable once CheckNamespace
namespace Bing.Trees;

/// <summary>
/// 树型表格结果
/// </summary>
public interface ITreeTableResult<TNode> where TNode : TreeDto<TNode>
{
    /// <summary>
    /// 获取树型表格结果
    /// </summary>
    /// <returns>树型表格节点结果列表。</returns>
    List<TNode> GetResult();
}