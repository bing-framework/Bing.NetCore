using System;
using System.ComponentModel.DataAnnotations;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Application.Dtos;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Dtos.Systems
{
    /// <summary>
    /// 应用程序数据传输对象
    /// </summary>
    public class ApplicationDto : DtoBase
    {
        /// <summary>
        /// 应用程序类型
        /// </summary>
        [Display(Name = "应用程序类型")]
        public ApplicationType ApplicationType { get; set; }

        /// <summary>
        /// 应用程序类型
        /// </summary>
        [Display(Name = "应用程序类型")]
        public string ApplicationTypeDesc => ApplicationType.Description();

        /// <summary>
        /// 应用程序编码
        /// </summary>
        [Required(ErrorMessage = "应用程序编码不能为空")]
        [StringLength(50, ErrorMessage = "应用程序编码输入过长，不能超过50位")]
        [Display(Name = "应用程序编码")]
        public string Code { get; set; }
        /// <summary>
        /// 应用程序名称
        /// </summary>
        [Required(ErrorMessage = "应用程序名称不能为空")]
        [StringLength(250, ErrorMessage = "应用程序名称输入过长，不能超过250位")]
        [Display(Name = "应用程序名称")]
        public string Name { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }
        /// <summary>
        /// 启用注册
        /// </summary>
        [Display(Name = "启用注册")]
        public bool? RegisterEnabled { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        [Display(Name = "备注")]
        public string Remark { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [Display(Name = "是否删除")]
        public bool? IsDeleted { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display(Name = "版本号")]
        public byte[] Version { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人标识
        /// </summary>
        [Display(Name = "创建人标识")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [StringLength(200, ErrorMessage = "创建人输入过长，不能超过200位")]
        [Display(Name = "创建人")]
        public string Creator { get; set; }
    }
}
