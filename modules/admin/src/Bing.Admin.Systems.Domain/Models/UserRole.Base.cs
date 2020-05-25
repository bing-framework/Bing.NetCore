using System;
using System.ComponentModel.DataAnnotations;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 用户角色
    /// </summary>
    [Display(Name = "用户角色")]
    public partial class UserRole
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 角色编号
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// 初始化一个<see cref="UserRole"/>类型的实例
        /// </summary>
        public UserRole() { }

        /// <summary>
        /// 初始化一个<see cref="UserRole"/>类型的实例
        /// </summary>
        /// <param name="userId">用户编号</param>
        /// <param name="roleId">角色编号</param>
        public UserRole(Guid userId, Guid roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }
    }
}
