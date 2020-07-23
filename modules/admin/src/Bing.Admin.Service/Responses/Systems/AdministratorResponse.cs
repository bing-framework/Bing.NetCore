using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Applications.Dtos;
using Bing.Biz.Enums;
using Bing.Extensions;

namespace Bing.Admin.Service.Responses.Systems
{
    /// <summary>
    /// 管理员 响应
    /// </summary>
    public class AdministratorResponse : DtoBase
    {
        /// <summary>
        /// 昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public Gender? Gender { get; set; }

        /// <summary>
        /// 性别显示文本
        /// </summary>
        public string GenderDesc => Gender?.Description() ?? string.Empty;

        /// <summary>
        /// 启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        public string LastModifier { get; set; }

        /// <summary>
        /// 角色
        /// </summary>
        public List<UserRoleResponse> Roles { get; set; }

        /// <summary>
        /// 角色显示文本
        /// </summary>
        public string RoleDesc => Roles?.Select(x => x.Name).Join();
    }
}
