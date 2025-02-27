using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Auditing;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 应用程序
/// </summary>
[Display(Name = "应用程序")]
public partial class Application : Bing.Permissions.Identity.Models.ApplicationBase<Application, Guid>, IHasCreator, IHasModifier
{
    /// <summary>
    /// 初始化一个<see cref="Application"/>类型的实例
    /// </summary>
    public Application() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="Application"/>类型的实例
    /// </summary>
    /// <param name="id">应用程序标识</param>
    public Application(Guid id) : base(id) { }


    /// <summary>
    /// 创建人
    ///</summary>
    [Display(Name = "创建人")]
    [StringLength(200, ErrorMessage = "创建人输入过长，不能超过200位")]
    public string Creator { get; set; }

    /// <summary>
    /// 最后修改人
    ///</summary>
    [Display(Name = "最后修改人")]
    [StringLength(200, ErrorMessage = "最后修改人输入过长，不能超过200位")]
    public string LastModifier { get; set; }

    /// <summary>
    /// 应用程序类型
    /// </summary>
    [DisplayName("应用程序类型")]
    public ApplicationType ApplicationType { get; set; }
}
