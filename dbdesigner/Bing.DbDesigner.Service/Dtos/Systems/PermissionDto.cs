using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Systems {
    /// <summary>
    /// 权限数据传输对象
    /// </summary>
    public class PermissionDto : DtoBase {
        /// <summary>
        /// 角色标识
        /// </summary>
        [Display( Name = "角色标识" )]
        [DataMember]
        public Guid? RoleId { get; set; }
        /// <summary>
        /// 资源标识
        /// </summary>
        [Display( Name = "资源标识" )]
        [DataMember]
        public Guid ResourceId { get; set; }
        /// <summary>
        /// 拒绝
        /// </summary>
        [Display( Name = "拒绝" )]
        [DataMember]
        public bool? IsDeny { get; set; }
        /// <summary>
        /// 签名
        /// </summary>
        [StringLength( 50, ErrorMessage = "签名输入过长，不能超过50位" )]
        [Display( Name = "签名" )]
        [DataMember]
        public string Sign { get; set; }
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