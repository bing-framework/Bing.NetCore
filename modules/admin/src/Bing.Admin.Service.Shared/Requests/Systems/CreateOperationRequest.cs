using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 创建操作请求
    /// </summary>
    public class CreateOperationRequest : RequestBase
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// 模块标识
        /// </summary>
        [Required(ErrorMessage = "模块标识不能为空")]
        public Guid ModuleId { get; set; }

        /// <summary>
        /// 操作编码
        /// </summary>
        [Required(ErrorMessage = "操作编码不能为空")]
        [StringLength(300, ErrorMessage = "编码输入过长，不能超过300位")]
        public string Code { get; set; }

        /// <summary>
        /// 操作名称
        /// </summary>
        [Required(ErrorMessage = "操作名称不能为空")]
        [StringLength(200, ErrorMessage = "操作名称输入过长，不能超过200位")]
        public string Name { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
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
