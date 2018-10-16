using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;
using Bing.Applications.Trees;

namespace Bing.DbDesigner.Service.Dtos.Commons {
    /// <summary>
    /// 字典数据传输对象
    /// </summary>
    public class DictionaryDto : TreeDtoBase {
        /// <summary>
        /// 编码
        /// </summary>
        [StringLength( 100, ErrorMessage = "编码输入过长，不能超过100位" )]
        [Display( Name = "编码" )]
        [DataMember]
        public string Code { get; set; }
        /// <summary>
        /// 文本
        /// </summary>
        [Required(ErrorMessage = "文本不能为空")]
        [StringLength( 200, ErrorMessage = "文本输入过长，不能超过200位" )]
        [Display( Name = "文本" )]
        [DataMember]
        public string Text { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 50, ErrorMessage = "拼音简码输入过长，不能超过50位" )]
        [Display( Name = "拼音简码" )]
        [DataMember]
        public string PinYin { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        [Display( Name = "备注" )]
        [DataMember]
        public string Note { get; set; }
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
        /// 租户标识
        /// </summary>
        [StringLength( 50, ErrorMessage = "租户标识输入过长，不能超过50位" )]
        [Display( Name = "租户标识" )]
        [DataMember]
        public string TenantId { get; set; }
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