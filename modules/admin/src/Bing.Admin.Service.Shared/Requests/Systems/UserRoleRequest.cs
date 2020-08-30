using System;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 用户角色请求
    /// </summary>
    public class UserRoleRequest : RequestBase
    {
        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// 用户标识列表。多个Id用逗号分隔。范例：1,2,3
        /// </summary>
        public string UserIds { get; set; }
    }
}
