﻿using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 权限
/// </summary>
[Display(Name = "权限")]
public partial class Permission : AggregateRoot<Permission>,ISoftDelete, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="Permission"/>类型的实例
    /// </summary>
    public Permission() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="Permission"/>类型的实例
    /// </summary>
    /// <param name="id">权限标识</param>
    public Permission(Guid id) : base(id) { }

    /// <summary>
    /// 角色标识
    ///</summary>
    [Display(Name = "角色标识")]
    [Required(ErrorMessage = "角色标识不能为空")]
    public Guid RoleId { get; set; }
    
    /// <summary>
    /// 资源标识
    ///</summary>
    [Display(Name = "资源标识")]
    [Required(ErrorMessage = "资源标识不能为空")]
    public Guid ResourceId { get; set; }
    
    /// <summary>
    /// 拒绝
    ///</summary>
    [Display(Name = "拒绝")]
    public bool IsDeny { get; set; }
    
    /// <summary>
    /// 签名
    ///</summary>
    [Display(Name = "签名")]
    [StringLength( 256, ErrorMessage = "签名输入过长，不能超过256位" )]
    public string Sign { get; set; }
    
    /// <summary>
    /// 是否删除
    ///</summary>
    [Display(Name = "是否删除")]
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// 创建时间
    ///</summary>
    [Display(Name = "创建时间")]
    public DateTime? CreationTime { get; set; }
    
    /// <summary>
    /// 创建人标识
    ///</summary>
    [Display(Name = "创建人标识")]
    public Guid? CreatorId { get; set; }
    
    /// <summary>
    /// 创建人
    ///</summary>
    [Display(Name = "创建人")]
    [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
    public string Creator { get; set; }
    
    /// <summary>
    /// 最后修改时间
    ///</summary>
    [Display(Name = "最后修改时间")]
    public DateTime? LastModificationTime { get; set; }
    
    /// <summary>
    /// 最后修改人标识
    ///</summary>
    [Display(Name = "最后修改人标识")]
    public Guid? LastModifierId { get; set; }
    
    /// <summary>
    /// 最后修改人
    ///</summary>
    [Display(Name = "最后修改人")]
    [StringLength( 200, ErrorMessage = "最后修改人输入过长，不能超过200位" )]
    public string LastModifier { get; set; }
}
