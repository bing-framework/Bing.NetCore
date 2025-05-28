using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Bing.Auditing;
using Bing.Data;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 用户
/// </summary>
[Display(Name = "用户")]
public partial class User : Bing.Permissions.Identity.Models.UserBase<User, Guid>, ISoftDelete, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="User"/>类型的实例
    /// </summary>
    public User() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="User"/>类型的实例
    /// </summary>
    /// <param name="id">用户标识</param>
    public User(Guid id) : base(id)
    {
        _claims = new List<Claim>();
    }

    /// <summary>
    /// 名称
    ///</summary>
    [Display(Name = "名称")]
    [StringLength( 256, ErrorMessage = "名称输入过长，不能超过256位" )]
    public string Name { get; set; }
    
    /// <summary>
    /// 创建人
    ///</summary>
    [Display(Name = "创建人")]
    [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
    public string Creator { get; set; }
    
    /// <summary>
    /// 最后修改人
    ///</summary>
    [Display(Name = "最后修改人")]
    [StringLength( 200, ErrorMessage = "最后修改人输入过长，不能超过200位" )]
    public string LastModifier { get; set; }
}
