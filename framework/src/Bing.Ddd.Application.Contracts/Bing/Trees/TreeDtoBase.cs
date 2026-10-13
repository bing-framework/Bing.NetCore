using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;


// ReSharper disable once CheckNamespace
namespace Bing.Trees;

/// <summary>
/// 提供树形数据传输对象的父节点、层级、路径和展示状态。
/// </summary>
public abstract class TreeDtoBase : DtoBase, ITreeNode
{
    /// <inheritdoc />
    public string ParentId { get; set; }

    /// <summary>
    /// 获取或设置父节点名称。
    /// </summary>
    public string ParentName { get; set; }

    /// <inheritdoc />
    public string Path { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时未指定层级。
    /// </remarks>
    public int? Level { get; set; }

    /// <summary>
    /// 获取或设置节点是否启用。
    /// </summary>
    /// <remarks>
    /// 默认值为 <see langword="true"/>。
    /// </remarks>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; } = true;

    /// <summary>
    /// 获取或设置同级节点的排序号。
    /// </summary>
    /// <remarks>
    /// 为空时不指定排序号。
    /// </remarks>
    [Display(Name = "排序号")]
    public int? SortId { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时未指定展开状态。
    /// </remarks>
    [Display(Name = "是否展开")]
    public bool? Expanded { get; set; }
}
