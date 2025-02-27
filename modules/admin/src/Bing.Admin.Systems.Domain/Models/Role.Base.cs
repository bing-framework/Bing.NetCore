using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 角色
/// </summary>
[Display(Name = "角色")]
public partial class Role : Bing.Permissions.Identity.Models.RoleBase<Role, Guid, Guid?>, ISoftDelete, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="Role"/>类型的实例
    /// </summary>
    public Role() : this(Guid.Empty, "", 0) { }

    /// <summary>
    /// 初始化一个<see cref="Role"/>类型的实例
    /// </summary>
    /// <param name="id">角色标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    public Role(Guid id, string path, int level) : base(id, path, level) { }

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
