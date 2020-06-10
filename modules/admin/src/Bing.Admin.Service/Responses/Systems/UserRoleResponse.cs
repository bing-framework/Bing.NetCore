using System;
using Bing.Applications.Dtos;
using Newtonsoft.Json;

namespace Bing.Admin.Service.Responses.Systems
{
    /// <summary>
    /// 用户角色响应
    /// </summary>
    public class UserRoleResponse : IResponse
    {
        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户标识
        /// </summary>
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>
        /// 角色编号
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }
    }
}
