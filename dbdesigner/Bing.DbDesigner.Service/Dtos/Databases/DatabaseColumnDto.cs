using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Databases {
    /// <summary>
    /// 数据列数据传输对象
    /// </summary>
    public class DatabaseColumnDto : DtoBase {
        /// <summary>
        /// 数据库标识
        /// </summary>
        [Required(ErrorMessage = "数据库标识不能为空")]
        [Display( Name = "数据库标识" )]
        [DataMember]
        public Guid DatabaseId { get; set; }
        /// <summary>
        /// 数据表标识
        /// </summary>
        [Required(ErrorMessage = "数据表标识不能为空")]
        [Display( Name = "数据表标识" )]
        [DataMember]
        public Guid DatabaseTableId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Required(ErrorMessage = "用户标识不能为空")]
        [Display( Name = "用户标识" )]
        [DataMember]
        public Guid UserId { get; set; }
        /// <summary>
        /// 编码
        /// </summary>
        [Required(ErrorMessage = "编码不能为空")]
        [StringLength( 250, ErrorMessage = "编码输入过长，不能超过250位" )]
        [Display( Name = "编码" )]
        [DataMember]
        public string Code { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 250, ErrorMessage = "名称输入过长，不能超过250位" )]
        [Display( Name = "名称" )]
        [DataMember]
        public string Name { get; set; }
        /// <summary>
        /// 注释
        /// </summary>
        [StringLength( 500, ErrorMessage = "注释输入过长，不能超过500位" )]
        [Display( Name = "注释" )]
        [DataMember]
        public string Comment { get; set; }
        /// <summary>
        /// 数据类型编码
        /// </summary>
        [Required(ErrorMessage = "数据类型编码不能为空")]
        [StringLength( 250, ErrorMessage = "数据类型编码输入过长，不能超过250位" )]
        [Display( Name = "数据类型编码" )]
        [DataMember]
        public string DataTypeCode { get; set; }
        /// <summary>
        /// 数据类型显示
        /// </summary>
        [Required(ErrorMessage = "数据类型显示不能为空")]
        [StringLength( 250, ErrorMessage = "数据类型显示输入过长，不能超过250位" )]
        [Display( Name = "数据类型显示" )]
        [DataMember]
        public string DataTyepShow { get; set; }
        /// <summary>
        /// 长度
        /// </summary>
        [Required(ErrorMessage = "长度不能为空")]
        [Display( Name = "长度" )]
        [DataMember]
        public int Length { get; set; }
        /// <summary>
        /// 小数位
        /// </summary>
        [Required(ErrorMessage = "小数位不能为空")]
        [Display( Name = "小数位" )]
        [DataMember]
        public int DecimalPlaces { get; set; }
        /// <summary>
        /// 是否主键
        /// </summary>
        [Display( Name = "是否主键" )]
        [DataMember]
        public bool? IsPrimaryKey { get; set; }
        /// <summary>
        /// 是否可空
        /// </summary>
        [Display( Name = "是否可空" )]
        [DataMember]
        public bool? IsNull { get; set; }
        /// <summary>
        /// 是否外键
        /// </summary>
        [Display( Name = "是否外键" )]
        [DataMember]
        public bool? IsForeignKey { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [Display( Name = "是否删除" )]
        [DataMember]
        public bool? IsDeleted { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display( Name = "版本号" )]
        [DataMember]
        public Byte[] Version { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display( Name = "创建时间" )]
        [DataMember]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display( Name = "创建人" )]
        [DataMember]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display( Name = "最后修改时间" )]
        [DataMember]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display( Name = "最后修改人" )]
        [DataMember]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 排序号
        /// </summary>
        [Display( Name = "排序号" )]
        [DataMember]
        public int? SortId { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        [Display( Name = "备注" )]
        [DataMember]
        public string Note { get; set; }
        /// <summary>
        /// 扩展
        /// </summary>
        [Display( Name = "扩展" )]
        [DataMember]
        public string Extend { get; set; }
    }
}