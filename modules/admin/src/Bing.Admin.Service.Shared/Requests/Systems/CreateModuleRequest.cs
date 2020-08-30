using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 创建模块请求
    /// </summary>
    public class CreateModuleRequest : RequestBase
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// 父标识
        /// </summary>
        public Guid? ParentId { get; set; }

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
        [StringLength(300, ErrorMessage = "模块地址输入过长，不能超过300位")]
        [Display(Name = "模块地址")]
        public string Url { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        [Display(Name = "图标")]
        public string Icon { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        [Display(Name = "备注")]
        public string Remark { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        [Display(Name = "排序号")]
        public int? SortId { get; set; }
    }
}
