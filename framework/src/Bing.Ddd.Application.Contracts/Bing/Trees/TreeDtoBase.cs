using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;


// ReSharper disable once CheckNamespace
namespace Bing.Trees;

/// <summary>
/// 提供树形数据传输对象的父节点、路径、层级、启用和展示状态字段。
/// </summary>
public abstract class TreeDtoBase : DtoBase, ITreeNode
{
    /// <summary>
    /// 获取或设置父节点标识。
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// 获取或设置父节点名称。
    /// </summary>
    public string ParentName { get; set; }

    /// <summary>
    /// 获取或设置节点的物化路径。
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 获取或设置节点在树中的层级；为空时未指定层级。
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// 获取或设置节点是否启用，默认值为 <see langword="true"/>。
    /// </summary>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; } = true;

    /// <summary>
    /// 获取或设置同级节点的排序号；为空时不指定排序号。
    /// </summary>
    [Display(Name = "排序号")]
    public int? SortId { get; set; }

    /// <summary>
    /// 获取或设置节点是否在树形界面中展开；为空时未指定展开状态。
    /// </summary>
    [Display(Name = "是否展开")]
    public bool? Expanded { get; set; }
}