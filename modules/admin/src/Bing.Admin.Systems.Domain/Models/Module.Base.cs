﻿using System;
using System.ComponentModel.DataAnnotations;
using Bing.Trees;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 模块
/// </summary>
[Display(Name = "模块")]
public partial class Module : TreeEntityBase<Module>
{
    /// <summary>
    /// 初始化一个<see cref="Module"/>类型的实例
    /// </summary>
    public Module() : this(Guid.Empty, "", 0)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Module"/>类型的实例
    /// </summary>
    /// <param name="id">模块标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    public Module(Guid id, string path, int level) : base(id, path, level)
    {
    }

    /// <summary>
    /// 应用程序标识
    /// </summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [Required(ErrorMessage = "模块名称不能为空")]
    [StringLength(200, ErrorMessage = "模块名称输入过长，不能超过200位")]
    public string Name { get; set; }

    /// <summary>
    /// 模块地址
    /// </summary>
    [StringLength(300, ErrorMessage = "模块地址输入过长，不能超过300位")]
    public string Url { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 是否展开
    /// </summary>
    public bool? Expanded { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
    public string Remark { get; set; }

    /// <summary>
    /// 拼音简码
    /// </summary>
    [StringLength(200, ErrorMessage = "拼音简码输入过长，不能超过200位")]
    public string PinYin { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreationTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改人
    /// </summary>
    public Guid? LastModifierId { get; set; }
}
