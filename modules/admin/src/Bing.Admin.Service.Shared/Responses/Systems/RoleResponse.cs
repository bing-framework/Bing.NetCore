using System;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Responses.Systems
{
    /// <summary>
    /// 角色响应
    /// </summary>
    public class RoleResponse : IResponse
    {
        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 角色编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 标准化角色名称
        /// </summary>
        public string NormalizedName { get; set; }

        /// <summary>
        /// 是否管理员角色
        /// </summary>
        public bool? IsAdmin { get; set; }

        /// <summary>
        /// 是否默认角色
        /// </summary>
        public bool? IsDefault { get; set; }

        /// <summary>
        /// 是否系统角色
        /// </summary>
        public bool? IsSystem { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }
}
