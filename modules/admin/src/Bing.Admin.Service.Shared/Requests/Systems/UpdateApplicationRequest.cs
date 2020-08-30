using System.ComponentModel.DataAnnotations;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 修改应用程序请求
    /// </summary>
    public class UpdateApplicationRequest : RequestBase
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Required(ErrorMessage = "应用程序标识不能为空")]
        public string Id { get; set; }

        /// <summary>
        /// 应用程序类型
        /// </summary>
        [Display(Name = "应用程序类型")]
        public ApplicationType ApplicationType { get; set; }
        /// <summary>
        /// 应用程序编码
        /// </summary>
        [Required(ErrorMessage = "应用程序编码不能为空")]
        [StringLength(50, ErrorMessage = "应用程序编码输入过长，不能超过50位")]
        [Display(Name = "应用程序编码")]
        [RegularExpression("^[a-zA-Z0-9\\.]+$", ErrorMessage = "应用程序编码只能填写英文字母、数字和.（点）")]
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
    }
}
