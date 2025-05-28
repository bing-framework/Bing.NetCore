using System;
using System.ComponentModel.DataAnnotations;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 操作
/// </summary>
[Display(Name = "操作")]
public partial class Operation : AggregateRoot<Operation>
{
    /// <summary>
    /// 初始化一个<see cref="Operation" />类型的实例
    /// </summary>
    public Operation() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="Operation" />类型的实例
    /// </summary>
    /// <param name="id">操作标识</param>
    public Operation(Guid id) : base(id)
    {
    }

    /// <summary>
    /// 应用程序标识
    /// </summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>
    /// 模块标识
    /// </summary>
    [Required(ErrorMessage = "模块标识不能为空")]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// 操作编码
    /// </summary>
    [Display(Name = "操作编码")]
    [Required(ErrorMessage = "操作编码不能为空")]
    [StringLength(300, ErrorMessage = "编码输入过长，不能超过300位")]
    public string Code { get; set; }

    /// <summary>
    /// 操作名称
    /// </summary>
    [Required(ErrorMessage = "操作名称不能为空")]
    [StringLength(200, ErrorMessage = "操作名称输入过长，不能超过200位")]
    public string Name { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; }

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
    /// 启用
    /// </summary>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int? SortId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreationTime { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改人
    /// </summary>
    public Guid? LastModifierId { get; set; }
}
