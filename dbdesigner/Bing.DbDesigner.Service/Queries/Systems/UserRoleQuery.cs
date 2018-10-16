using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Systems {
    /// <summary>
    /// 用户角色查询参数
    /// </summary>
    public class UserRoleQuery : QueryParameter {
        /// <summary>
        /// 角色标识
        /// </summary>
        [Display(Name="角色标识")]
        public Guid? RoleId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
    }
}