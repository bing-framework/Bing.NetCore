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
    /// 项目
    /// </summary>
    [DisplayName( "项目" )]
    public partial class Project : AggregateRoot<Project>,IDelete,IAudited {
        /// <summary>
        /// 初始化项目
        /// </summary>
        public Project() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化项目
        /// </summary>
        /// <param name="id">项目标识</param>
        public Project( Guid id ) : base( id ) {
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
        /// 编码
        /// </summary>
        [DisplayName( "编码" )]
        [Required(ErrorMessage = "编码不能为空")]
        [StringLength( 50, ErrorMessage = "编码输入过长，不能超过50位" )]
        public string Code { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        [DisplayName( "名称" )]
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
        public string Name { get; set; }
        /// <summary>
        /// 缩写
        /// </summary>
        [DisplayName( "缩写" )]
        [StringLength( 200, ErrorMessage = "缩写输入过长，不能超过200位" )]
        public string Addreviation { get; set; }
        /// <summary>
        /// 授权码
        /// </summary>
        [DisplayName( "授权码" )]
        [StringLength( 50, ErrorMessage = "授权码输入过长，不能超过50位" )]
        public string AuthCode { get; set; }
        /// <summary>
        /// 授权密钥
        /// </summary>
        [DisplayName( "授权密钥" )]
        [StringLength( 200, ErrorMessage = "授权密钥输入过长，不能超过200位" )]
        public string AuthKey { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [DisplayName( "启用" )]
        public bool Enabled { get; set; }
        /// <summary>
        /// 排序号
        /// </summary>
        [DisplayName( "排序号" )]
        public int? SortId { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [DisplayName( "备注" )]
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        public string Note { get; set; }
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
        /// 是否删除
        /// </summary>
        [DisplayName( "是否删除" )]
        public bool IsDeleted { get; set; }
        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.SolutionId );
            AddDescription( t => t.UserId );
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.Addreviation );
            AddDescription( t => t.AuthCode );
            AddDescription( t => t.AuthKey );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.SortId );
            AddDescription( t => t.Note );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( Project other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.SolutionId, other.SolutionId );
            AddChange( t => t.UserId, other.UserId );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Addreviation, other.Addreviation );
            AddChange( t => t.AuthCode, other.AuthCode );
            AddChange( t => t.AuthKey, other.AuthKey );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.Note, other.Note );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
        }
    }
}