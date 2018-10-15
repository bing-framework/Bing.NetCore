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

namespace Bing.DbDesigner.Databases.Domain.Models {
    /// <summary>
    /// 数据列
    /// </summary>
    [DisplayName( "数据列" )]
    public partial class DatabaseColumn : AggregateRoot<DatabaseColumn>,IDelete,IAudited {
        /// <summary>
        /// 初始化数据列
        /// </summary>
        public DatabaseColumn() : this( Guid.Empty ) {
        }

        /// <summary>
        /// 初始化数据列
        /// </summary>
        /// <param name="id">数据列标识</param>
        public DatabaseColumn( Guid id ) : base( id ) {
        }

        /// <summary>
        /// 数据库标识
        /// </summary>
        [DisplayName( "数据库标识" )]
        [Required(ErrorMessage = "数据库标识不能为空")]
        public Guid DatabaseId { get; set; }
        /// <summary>
        /// 数据表标识
        /// </summary>
        [DisplayName( "数据表标识" )]
        [Required(ErrorMessage = "数据表标识不能为空")]
        public Guid DatabaseTableId { get; set; }
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
        [StringLength( 250, ErrorMessage = "编码输入过长，不能超过250位" )]
        public string Code { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        [DisplayName( "名称" )]
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 250, ErrorMessage = "名称输入过长，不能超过250位" )]
        public string Name { get; set; }
        /// <summary>
        /// 注释
        /// </summary>
        [DisplayName( "注释" )]
        [StringLength( 500, ErrorMessage = "注释输入过长，不能超过500位" )]
        public string Comment { get; set; }
        /// <summary>
        /// 数据类型编码
        /// </summary>
        [DisplayName( "数据类型编码" )]
        [Required(ErrorMessage = "数据类型编码不能为空")]
        [StringLength( 250, ErrorMessage = "数据类型编码输入过长，不能超过250位" )]
        public string DataTypeCode { get; set; }
        /// <summary>
        /// 数据类型显示
        /// </summary>
        [DisplayName( "数据类型显示" )]
        [Required(ErrorMessage = "数据类型显示不能为空")]
        [StringLength( 250, ErrorMessage = "数据类型显示输入过长，不能超过250位" )]
        public string DataTyepShow { get; set; }
        /// <summary>
        /// 长度
        /// </summary>
        [DisplayName( "长度" )]
        [Required(ErrorMessage = "长度不能为空")]
        public int Length { get; set; }
        /// <summary>
        /// 小数位
        /// </summary>
        [DisplayName( "小数位" )]
        [Required(ErrorMessage = "小数位不能为空")]
        public int DecimalPlaces { get; set; }
        /// <summary>
        /// 是否主键
        /// </summary>
        [DisplayName( "是否主键" )]
        public bool IsPrimaryKey { get; set; }
        /// <summary>
        /// 是否可空
        /// </summary>
        [DisplayName( "是否可空" )]
        public bool IsNull { get; set; }
        /// <summary>
        /// 是否外键
        /// </summary>
        [DisplayName( "是否外键" )]
        public bool IsForeignKey { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [DisplayName( "是否删除" )]
        public bool IsDeleted { get; set; }
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
        /// 扩展
        /// </summary>
        [DisplayName( "扩展" )]
        public string Extend { get; set; }
        
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions() {
            AddDescription( t => t.Id );
            AddDescription( t => t.DatabaseId );
            AddDescription( t => t.DatabaseTableId );
            AddDescription( t => t.UserId );
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.Comment );
            AddDescription( t => t.DataTypeCode );
            AddDescription( t => t.DataTyepShow );
            AddDescription( t => t.Length );
            AddDescription( t => t.DecimalPlaces );
            AddDescription( t => t.IsPrimaryKey );
            AddDescription( t => t.IsNull );
            AddDescription( t => t.IsForeignKey );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
            AddDescription( t => t.SortId );
            AddDescription( t => t.Note );
            AddDescription( t => t.Extend );
        }
        
        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( DatabaseColumn other ) {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.DatabaseId, other.DatabaseId );
            AddChange( t => t.DatabaseTableId, other.DatabaseTableId );
            AddChange( t => t.UserId, other.UserId );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Comment, other.Comment );
            AddChange( t => t.DataTypeCode, other.DataTypeCode );
            AddChange( t => t.DataTyepShow, other.DataTyepShow );
            AddChange( t => t.Length, other.Length );
            AddChange( t => t.DecimalPlaces, other.DecimalPlaces );
            AddChange( t => t.IsPrimaryKey, other.IsPrimaryKey );
            AddChange( t => t.IsNull, other.IsNull );
            AddChange( t => t.IsForeignKey, other.IsForeignKey );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.Note, other.Note );
            AddChange( t => t.Extend, other.Extend );
        }
    }
}