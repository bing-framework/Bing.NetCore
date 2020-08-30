using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Dtos.Systems
{
    /// <summary>
    /// 模块参数
    /// </summary>
    public class ModuleDto : TreeDto<ModuleDto>
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Display(Name = "应用程序标识")]
        public Guid? ApplicationId { get; set; }
        /// <summary>
        /// 应用程序
        /// </summary>
        [Display(Name = "应用程序")]
        public string ApplicationName { get; set; }
        /// <summary>
        /// 模块名称
        /// </summary>
        [Required(ErrorMessage = "模块名称不能为空")]
        [StringLength(200)]
        [Display(Name = "模块名称")]
        public string Name { get; set; }
        /// <summary>
        /// 模块地址
        /// </summary>
        [StringLength(300)]
        [Display(Name = "模块地址")]
        public string Url { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500)]
        [Display(Name = "备注")]
        public string Remark { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name = "创建人")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display(Name = "版本号")]
        public byte[] Version { get; set; }
    }
}
