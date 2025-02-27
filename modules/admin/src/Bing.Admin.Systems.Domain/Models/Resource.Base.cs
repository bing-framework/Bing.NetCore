using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Trees;

namespace Bing.Admin.Systems.Domain.Models;

/// <summary>
/// 资源
/// </summary>
[Display(Name = "资源")]
public partial class Resource : TreeEntityBase<Resource>,ISoftDelete, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个<see cref="Resource"/>类型的实例
    /// </summary>
    public Resource() : this(Guid.Empty, "", 0) { }

    /// <summary>
    /// 初始化一个<see cref="Resource"/>类型的实例
    /// </summary>
    /// <param name="id">资源标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    public Resource(Guid id, string path, int level) : base(id, path, level) { }

    /// <summary>
    /// 应用程序标识
    ///</summary>
    [Display(Name = "应用程序标识")]
    [Required(ErrorMessage = "应用程序标识不能为空")]
    public Guid ApplicationId { get; set; }
    
    /// <summary>
    /// 资源识别号
    ///</summary>
    [Display(Name = "资源识别号")]
    [StringLength( 300, ErrorMessage = "资源识别号输入过长，不能超过300位" )]
    public string Uri { get; set; }
    
    /// <summary>
    /// 资源名称
    ///</summary>
    [Display(Name = "资源名称")]
    [Required(ErrorMessage = "资源名称不能为空")]
    [StringLength( 200, ErrorMessage = "资源名称输入过长，不能超过200位" )]
    public string Name { get; set; }
    
    /// <summary>
    /// 资源类型
    ///</summary>
    [Display(Name = "资源类型")]
    [Required(ErrorMessage = "资源类型不能为空")]
    public int Type { get; set; }
    
    /// <summary>
    /// 备注
    ///</summary>
    [Display(Name = "备注")]
    [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
    public string Remark { get; set; }
    
    /// <summary>
    /// 拼音简码
    ///</summary>
    [Display(Name = "拼音简码")]
    [Required(ErrorMessage = "拼音简码不能为空")]
    [StringLength( 50, ErrorMessage = "拼音简码输入过长，不能超过50位" )]
    public string PinYin { get; set; }
    
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
    
    /// <summary>
    /// 扩展
    ///</summary>
    [Display(Name = "扩展")]
    public string Extend { get; set; }
}
