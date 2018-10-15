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
    /// 用户解决方案
    /// </summary>
    [DisplayName( "用户解决方案" )]
    public partial class UserSolution : AggregateRoot<UserSolution>,IAudited {
        /// <summary>
        /// 初始化用户解决方案
        /// </summary>
        public UserSolution() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化用户解决方案
        /// </summary>
        /// <param name="id">用户解决方案标识</param>
        public UserSolution( Guid id ) : base( id ) {
        }

        /// <summary>
        /// 解决方案标识
        /// </summary>
        [DisplayName( "解决方案标识" )]
        [Required(ErrorMessage = "解决方案标识不能为空")]
        public Guid SolutionId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [DisplayName( "用户标识" )]
        [Required(ErrorMessage = "用户标识不能为空")]
        public Guid UserId { get; set; }
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
            AddDescription( t => t.SolutionId );
            AddDescription( t => t.UserId );
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
        protected override void AddChanges( UserSolution other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.SolutionId, other.SolutionId );
            AddChange( t => t.UserId, other.UserId );
            AddChange( t => t.IsAdmin, other.IsAdmin );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
        }
    }
}