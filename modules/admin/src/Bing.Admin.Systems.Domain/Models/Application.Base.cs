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
using Bing.Domains.Entities.Tenants;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 应用程序
    /// </summary>
    [Display(Name = "应用程序")]
    public partial class Application : AggregateRoot<Application>,IDelete,IAuditedWithNameObject
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
        /// 应用程序编码
        ///</summary>
        [Display(Name = "应用程序编码")]
        [Required(ErrorMessage = "应用程序编码不能为空")]
        [StringLength( 50, ErrorMessage = "应用程序编码输入过长，不能超过50位" )]
        public string Code { get; set; }
    
        /// <summary>
        /// 应用程序名称
        ///</summary>
        [Display(Name = "应用程序名称")]
        [Required(ErrorMessage = "应用程序名称不能为空")]
        [StringLength( 250, ErrorMessage = "应用程序名称输入过长，不能超过250位" )]
        public string Name { get; set; }
    
        /// <summary>
        /// 启用注册
        ///</summary>
        [Display(Name = "启用注册")]
        public bool RegisterEnabled { get; set; }
    
        /// <summary>
        /// 启用
        ///</summary>
        [Display(Name = "启用")]
        public bool Enabled { get; set; }
    
        /// <summary>
        /// 备注
        ///</summary>
        [Display(Name = "备注")]
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        public string Remark { get; set; }
    
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
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.RegisterEnabled );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.Remark );
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
        protected override void AddChanges( Application other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.RegisterEnabled, other.RegisterEnabled );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.Remark, other.Remark );
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