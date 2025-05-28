﻿using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Domain.Entities;

namespace Bing.Admin.Commons.Domain.Models;

/// <summary>
/// 文件
/// </summary>
[Display(Name = "文件")]
public partial class File : AggregateRoot<File>, ICreationAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="File"/>类型的实例
    /// </summary>
    public File() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="File"/>类型的实例
    /// </summary>
    /// <param name="id">文件标识</param>
    public File(Guid id) : base(id) { }

    /// <summary>
    /// 名称
    ///</summary>
    [Display(Name = "名称")]
    [Required(ErrorMessage = "名称不能为空")]
    [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
    public string Name { get; set; }
    
    /// <summary>
    /// 大小
    ///</summary>
    [Display(Name = "大小")]
    [Required(ErrorMessage = "大小不能为空")]
    public long Size { get; set; }
    
    /// <summary>
    /// 大小说明
    ///</summary>
    [Display(Name = "大小说明")]
    [StringLength( 50, ErrorMessage = "大小说明输入过长，不能超过50位" )]
    public string SizeExplain { get; set; }
    
    /// <summary>
    /// 扩展名
    ///</summary>
    [Display(Name = "扩展名")]
    [Required(ErrorMessage = "扩展名不能为空")]
    [StringLength( 10, ErrorMessage = "扩展名输入过长，不能超过10位" )]
    public string ExtName { get; set; }
    
    /// <summary>
    /// 地址
    ///</summary>
    [Display(Name = "地址")]
    [Required(ErrorMessage = "地址不能为空")]
    public string Address { get; set; }
    
    /// <summary>
    /// 创建时间
    ///</summary>
    [Display(Name = "创建时间")]
    public DateTime? CreationTime { get; set; }
    
    /// <summary>
    /// 创建人编号
    ///</summary>
    [Display(Name = "创建人编号")]
    public Guid? CreatorId { get; set; }
    
    /// <summary>
    /// 创建人
    ///</summary>
    [Display(Name = "创建人")]
    [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
    public string Creator { get; set; }
}
