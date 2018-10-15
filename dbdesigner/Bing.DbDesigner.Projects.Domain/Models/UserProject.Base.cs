using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Tenants;

namespace Bing.DbDesigner.Projects.Domain.Models {
    /// <summary>
    /// 用户项目
    /// </summary>
    [DisplayName( "用户项目" )]
    public partial class UserProject : AggregateRoot<UserProject>,IAudited {
        /// <summary>
        /// 初始化用户项目
        /// </summary>
        public UserProject() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化用户项目
        /// </summary>
        /// <param name="id">用户项目标识</param>
        public UserProject( Guid id ) : base( id ) {
        }

        /// <summary>
        /// 用户标识
        /// </summary>
        [DisplayName( "用户标识" )]
        [Required(ErrorMessage = "用户标识不能为空")]
        public Guid UserId { get; set; }
        /// <summary>
        /// 项目标识
        /// </summary>
        [DisplayName( "项目标识" )]
        [Required(ErrorMessage = "项目标识不能为空")]
        public Guid ProjectId { get; set; }
        /// <summary>
        /// 是否管理员
        /// </summary>
        [DisplayName( "是否管理员" )]
        public bool IsAdmin { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [DisplayName( "启用" )]
        public bool Enabled { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [DisplayName( "创建时间" )]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [DisplayName( "创建人" )]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [DisplayName( "最后修改时间" )]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [DisplayName( "最后修改人" )]
        public Guid? LastModifierId { get; set; }
        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.UserId );
            AddDescription( t => t.ProjectId );
            AddDescription( t => t.IsAdmin );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( UserProject other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.UserId, other.UserId );
            AddChange( t => t.ProjectId, other.ProjectId );
            AddChange( t => t.IsAdmin, other.IsAdmin );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
        }
    }
}