using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Projects {
    /// <summary>
    /// 用户项目数据传输对象
    /// </summary>
    public class UserProjectDto : DtoBase {
        /// <summary>
        /// 用户标识
        /// </summary>
        [Required(ErrorMessage = "用户标识不能为空")]
        [Display( Name = "用户标识" )]
        [DataMember]
        public Guid UserId { get; set; }
        /// <summary>
        /// 项目标识
        /// </summary>
        [Required(ErrorMessage = "项目标识不能为空")]
        [Display( Name = "项目标识" )]
        [DataMember]
        public Guid ProjectId { get; set; }
        /// <summary>
        /// 是否管理员
        /// </summary>
        [Display( Name = "是否管理员" )]
        [DataMember]
        public bool? IsAdmin { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [Display( Name = "启用" )]
        [DataMember]
        public bool? Enabled { get; set; }
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
    }
}