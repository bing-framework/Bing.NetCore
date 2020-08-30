using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 角色信息
    /// </summary>
    public class RoleInfo
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
    }
}
