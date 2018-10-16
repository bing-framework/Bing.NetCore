using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Commons {
    /// <summary>
    /// 文件数据传输对象
    /// </summary>
    public class FileDto : DtoBase {
        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
        [Display( Name = "名称" )]
        [DataMember]
        public string Name { get; set; }
        /// <summary>
        /// 大小
        /// </summary>
        [Required(ErrorMessage = "大小不能为空")]
        [Display( Name = "大小" )]
        [DataMember]
        public long Size { get; set; }
        /// <summary>
        /// 大小说明
        /// </summary>
        [StringLength( 50, ErrorMessage = "大小说明输入过长，不能超过50位" )]
        [Display( Name = "大小说明" )]
        [DataMember]
        public string SizeExplain { get; set; }
        /// <summary>
        /// 扩展名
        /// </summary>
        [Required(ErrorMessage = "扩展名不能为空")]
        [StringLength( 10, ErrorMessage = "扩展名输入过长，不能超过10位" )]
        [Display( Name = "扩展名" )]
        [DataMember]
        public string Extensions { get; set; }
        /// <summary>
        /// 地址
        /// </summary>
        [Required(ErrorMessage = "地址不能为空")]
        [Display( Name = "地址" )]
        [DataMember]
        public string Address { get; set; }
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
        /// 版本号
        /// </summary>
        [Display( Name = "版本号" )]
        [DataMember]
        public Byte[] Version { get; set; }
    }
}