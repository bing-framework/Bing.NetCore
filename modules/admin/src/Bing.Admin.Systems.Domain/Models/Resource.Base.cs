using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Auditing;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Trees;
using Bing.Domains.Entities.Tenants;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 资源
    /// </summary>
    [Display(Name = "资源")]
    public partial class Resource : TreeEntityBase<Resource>,IDelete,IAuditedWithNameObject
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
    
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription( t => t.Id );
            AddDescription( t => t.ApplicationId );
            AddDescription( t => t.Uri );
            AddDescription( t => t.Name );
            AddDescription( t => t.Type );
            AddDescription( t => t.ParentId );
            AddDescription( t => t.Path );
            AddDescription( t => t.Level );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.SortId );
            AddDescription( t => t.Remark );
            AddDescription( t => t.PinYin );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.Creator );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
            AddDescription( t => t.LastModifier );
            AddDescription( t => t.Extend );
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( Resource other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.ApplicationId, other.ApplicationId );
            AddChange( t => t.Uri, other.Uri );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Type, other.Type );
            AddChange( t => t.ParentId, other.ParentId );
            AddChange( t => t.Path, other.Path );
            AddChange( t => t.Level, other.Level );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.Remark, other.Remark );
            AddChange( t => t.PinYin, other.PinYin );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.LastModifier, other.LastModifier );
            AddChange( t => t.Extend, other.Extend );
        }
    }
}