using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;
using Bing.Applications.Trees;

namespace Bing.DbDesigner.Service.Dtos.Systems {
    /// <summary>
    /// 资源数据传输对象
    /// </summary>
    public class ResourceDto : TreeDtoBase {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Display( Name = "应用程序标识" )]
        [DataMember]
        public Guid? ApplicationId { get; set; }
        /// <summary>
        /// 资源地址
        /// </summary>
        [StringLength( 300, ErrorMessage = "资源地址输入过长，不能超过300位" )]
        [Display( Name = "资源地址" )]
        [DataMember]
        public string Uri { get; set; }
        /// <summary>
        /// 资源名称
        /// </summary>
        [Required(ErrorMessage = "资源名称不能为空")]
        [StringLength( 200, ErrorMessage = "资源名称输入过长，不能超过200位" )]
        [Display( Name = "资源名称" )]
        [DataMember]
        public string Name { get; set; }
        /// <summary>
        /// 资源类型
        /// </summary>
        [Required(ErrorMessage = "资源类型不能为空")]
        [Display( Name = "资源类型" )]
        [DataMember]
        public int Type { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display( Name = "创建时间" )]
        [DataMember]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        [Display( Name = "备注" )]
        [DataMember]
        public string Note { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 200, ErrorMessage = "拼音简码输入过长，不能超过200位" )]
        [Display( Name = "拼音简码" )]
        [DataMember]
        public string PinYin { get; set; }
        /// <summary>
        /// 扩展
        /// </summary>
        [Display( Name = "扩展" )]
        [DataMember]
        public string Extend { get; set; }
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
    }
}