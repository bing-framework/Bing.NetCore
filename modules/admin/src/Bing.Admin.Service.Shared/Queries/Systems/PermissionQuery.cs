using System;
using Bing.Datas.Queries;

namespace Bing.Admin.Service.Shared.Queries.Systems
{
    /// <summary>
    /// 权限查询参数
    /// </summary>
    public class PermissionQuery : QueryParameter
    {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// 权限标识
        /// </summary>
        public Guid PermissionId { get; set; }

        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid? RoleId { get; set; }

        /// <summary>
        /// 资源标识
        /// </summary>
        public Guid ResourceId { get; set; }

        /// <summary>
        /// 拒绝
        /// </summary>
        public bool? IsDeny { get; set; }
    }
}
