using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;
using Newtonsoft.Json;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 修改角色请求
    /// </summary>
    public class UpdateRoleRequest : RequestBase
    {
        /// <summary>
        /// 角色标识
        /// </summary>
        [Required(ErrorMessage = "角色标识不能为空")]
        public string Id { get; set; }

        /// <summary>
        /// 角色编码
        /// </summary>
        [Required(ErrorMessage = "角色编码不能为空")]
        [StringLength(256)]
        [Display(Name = "角色编码")]
        [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "角色编码只能填写英文字母、数字和-（横杠）")]
        public string Code { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        [Required(ErrorMessage = "角色名称不能为空")]
        [StringLength(256)]
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
        /// 备注
        /// </summary>
        [StringLength(500)]
        [Display(Name = "备注")]
        public string Remark { get; set; }
    }
}
