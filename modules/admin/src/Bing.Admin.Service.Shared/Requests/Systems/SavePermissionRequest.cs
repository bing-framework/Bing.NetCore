using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 保存权限请求
    /// </summary>
    public class SavePermissionRequest : RequestBase
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// 角色标识
        /// </summary>
        [Required(ErrorMessage = "角色不能为空")]
        public Guid? RoleId { get; set; }

        /// <summary>
        /// 资源标识。多个Id用逗号分隔。范例：1,2,3
        /// </summary>
        public string ResourceIds { get; set; }

        /// <summary>
        /// 拒绝
        /// </summary>
        public bool? IsDeny { get; set; }
    }
}
