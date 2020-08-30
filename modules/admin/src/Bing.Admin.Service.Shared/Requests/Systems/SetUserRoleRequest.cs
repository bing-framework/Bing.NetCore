using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 设置用户角色请求
    /// </summary>
    public class SetUserRoleRequest : RequestBase
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// 角色标识列表。多个Id用逗号分隔。范例：1,2,3
        /// </summary>
        public string RoleIds { get; set; }
    }
}
