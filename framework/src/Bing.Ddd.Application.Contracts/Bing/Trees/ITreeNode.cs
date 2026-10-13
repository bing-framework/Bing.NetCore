using Bing.Application.Dtos;

namespace Bing.Trees;

/// <summary>
/// 定义树节点 DTO 的标识、层级、路径和展开状态契约。
/// </summary>
public interface ITreeNode : IKey
{
    /// <summary>
    /// 获取或设置父节点标识。
    /// </summary>
    string ParentId { get; set; }

    /// <summary>
    /// 获取或设置节点的物化路径。
    /// </summary>
    string Path { get; set; }

    /// <summary>
    /// 获取或设置节点在树中的层级。
    /// </summary>
    /// <remarks>
    /// 为空时未指定层级。
    /// </remarks>
    int? Level { get; set; }

    /// <summary>
    /// 获取或设置节点是否在树形界面中展开。
    /// </summary>
    /// <remarks>
    /// 为空时未指定展开状态。
    /// </remarks>
    bool? Expanded { get; set; }
}
