using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing.Utils;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 管理员
    /// </summary>
    [Display(Name = "管理员")]
    public partial class Administrator : AggregateRoot<Administrator>,ISoftDelete, IAuditedObjectWithName
    {
        /// <summary>
        /// 初始化一个<see cref="Administrator"/>类型的实例
        /// </summary>
        public Administrator() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="Administrator"/>类型的实例
        /// </summary>
        /// <param name="id">管理员标识</param>
        public Administrator(Guid id) : base(id) { }

        /// <summary>
        /// 名称
        ///</summary>
        [Display(Name = "名称")]
        [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
        public string Name { get; set; }
    
        /// <summary>
        /// 是否删除
        ///</summary>
        [Display(Name = "是否删除")]
        public bool IsDeleted { get; set; }
    
        /// <summary>
        /// 启用
        ///</summary>
        [Display(Name = "启用")]
        public bool Enabled { get; set; }
    
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
    
        /// <summary>
        /// 最后修改时间
        ///</summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }
    
        /// <summary>
        /// 最后修改人编号
        ///</summary>
        [Display(Name = "最后修改人编号")]
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
            AddDescription( t => t.Name );
            AddDescription( t => t.Enabled );
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
        protected override void AddChanges( Administrator other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Enabled, other.Enabled );
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
