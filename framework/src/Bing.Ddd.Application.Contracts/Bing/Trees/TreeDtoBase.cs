﻿using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;


// ReSharper disable once CheckNamespace
namespace Bing.Trees;

/// <summary>
/// 树型数据传输对象基类
/// </summary>
public abstract class TreeDtoBase : DtoBase, ITreeNode
{
    /// <summary>
    /// 父标识
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// 父名称
    /// </summary>
    public string ParentName { get; set; }

    /// <summary>
    /// 路径
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 层级
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// 启用
    /// </summary>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; } = true;

    /// <summary>
    /// 排序号
    /// </summary>
    [Display(Name = "排序号")]
    public int? SortId { get; set; }

    /// <summary>
    /// 是否展开
    /// </summary>
    [Display(Name = "是否展开")]
    public bool? Expanded { get; set; }
}