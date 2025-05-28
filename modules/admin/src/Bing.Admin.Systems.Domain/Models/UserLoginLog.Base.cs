using System;
using System.ComponentModel.DataAnnotations;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 用户登录日志
/// </summary>
[Display(Name = "用户登录日志")]
public partial class UserLoginLog : AggregateRoot<UserLoginLog>
{
    /// <summary>
    /// 初始化一个<see cref="UserLoginLog"/>类型的实例
    /// </summary>
    public UserLoginLog() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="UserLoginLog"/>类型的实例
    /// </summary>
    /// <param name="id">用户登录日志标识</param>
    public UserLoginLog(Guid id) : base(id) { }

    /// <summary>
    /// 用户编号
    ///</summary>
    [Display(Name = "用户编号")]
    [Required(ErrorMessage = "用户编号不能为空")]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// 名称
    ///</summary>
    [Display(Name = "名称")]
    [StringLength( 250, ErrorMessage = "名称输入过长，不能超过250位" )]
    public string Name { get; set; }
    
    /// <summary>
    /// 登录IP
    ///</summary>
    [Display(Name = "登录IP")]
    [StringLength( 30, ErrorMessage = "登录IP输入过长，不能超过30位" )]
    public string Ip { get; set; }
    
    /// <summary>
    /// 用户代理
    ///</summary>
    [Display(Name = "用户代理")]
    [StringLength( 1000, ErrorMessage = "用户代理输入过长，不能超过1000位" )]
    public string UserAgent { get; set; }
    
    /// <summary>
    /// 退出登录时间
    ///</summary>
    [Display(Name = "退出登录时间")]
    public DateTime? LogoutTime { get; set; }
    
    /// <summary>
    /// 创建时间
    ///</summary>
    [Display(Name = "创建时间")]
    public DateTime? CreationTime { get; set; }
}
