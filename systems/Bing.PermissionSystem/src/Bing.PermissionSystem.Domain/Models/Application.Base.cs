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

namespace Bing.PermissionSystem.Domain.Models {
    /// <summary>
    /// 应用程序
    /// </summary>
    [DisplayName( "应用程序" )]
    public partial class Application : AggregateRoot<Application>,IDelete,IAudited {
        /// <summary>
        /// 初始化应用程序
        /// </summary>
        public Application() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化应用程序
        /// </summary>
        /// <param name="id">应用程序标识</param>
        public Application( Guid id ) : base( id ) {
            Resources = new List<Resource>();
        }

        /// <summary>
        /// 应用程序编码
        /// </summary>
        [DisplayName( "应用程序编码" )]
        [Required(ErrorMessage = "应用程序编码不能为空")]
        [StringLength( 60, ErrorMessage = "应用程序编码输入过长，不能超过60位" )]
        public string Code { get; set; }
        /// <summary>
        /// 应用程序名称
        /// </summary>
        [DisplayName( "应用程序名称" )]
        [Required(ErrorMessage = "应用程序名称不能为空")]
        [StringLength( 200, ErrorMessage = "应用程序名称输入过长，不能超过200位" )]
        public string Name { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [DisplayName( "启用" )]
        public bool Enabled { get; set; }
        /// <summary>
        /// 启用注册
        /// </summary>
        [DisplayName( "启用注册" )]
        public bool RegisterEnabled { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [DisplayName( "备注" )]
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        public string Remark { get; set; }
        /// <summary>
        /// 扩展
        /// </summary>
        [DisplayName( "扩展" )]
        public string Extend { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [DisplayName( "创建时间" )]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人编号
        /// </summary>
        [DisplayName( "创建人编号" )]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [DisplayName( "最后修改时间" )]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人编号
        /// </summary>
        [DisplayName( "最后修改人编号" )]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [DisplayName( "是否删除" )]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 是否客户端
        /// </summary>
        [DisplayName("是否客户端")]
        public bool IsClient { get; set; }

        /// <summary>
        /// 客户端
        /// </summary>
        public Client Client { get; set; }
        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.RegisterEnabled );
            AddDescription( t => t.Remark );
            AddDescription( t => t.Extend );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( Application other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.RegisterEnabled, other.RegisterEnabled );
            AddChange( t => t.Remark, other.Remark );
            AddChange( t => t.Extend, other.Extend );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
        }
    }
}
