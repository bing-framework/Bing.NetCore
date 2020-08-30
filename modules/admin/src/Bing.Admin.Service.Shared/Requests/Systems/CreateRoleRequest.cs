using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;
using Newtonsoft.Json;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 创建角色请求
    /// </summary>
    public class CreateRoleRequest : RequestBase
    {
        /// <summary>
        /// 角色编码
        /// </summary>
        [Required(ErrorMessage = "角色编码不能为空")]
        [StringLength(50, ErrorMessage = "编码输入过长，不能超过50位")]
        [Display(Name = "角色编码")]
        [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "角色编码只能填写英文字母、数字和-（横杠）")]
        public string Code { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        [Required(ErrorMessage = "角色名称不能为空")]
        [StringLength(50, ErrorMessage = "名称输入过长，不能超过50位")]
        [Display(Name = "角色名称")]
        public string Name { get; set; }

        /// <summary>
        /// 是否管理员角色
        /// </summary>
        [Required(ErrorMessage = "是否管理员角色不能为空")]
        [Display(Name = "是否管理员角色")]
        [JsonIgnore]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// 是否默认角色
        /// </summary>
        [Required(ErrorMessage = "是否默认角色不能为空")]
        [Display(Name = "是否默认角色")]
        [JsonIgnore]
        public bool IsDefault { get; set; }

        /// <summary>
        /// 是否系统角色
        /// </summary>
        [Required(ErrorMessage = "是否系统角色不能为空")]
        [Display(Name = "是否系统角色")]
        [JsonIgnore]
        public bool IsSystem { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }

        /// <summary>
        /// 角色类型
        /// </summary>
        [StringLength(80)]
        [Display(Name = "角色类型")]
        [JsonIgnore]
        public string Type { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        [Display(Name = "备注")]
        public string Remark { get; set; }

        /// <summary>
        /// 租户标识
        /// </summary>
        [JsonIgnore]
        public string TenantId { get; set; }

        /// <summary>
        /// 父标识
        /// </summary>
        [JsonIgnore]
        public Guid? ParentId { get; set; }
    }
}
