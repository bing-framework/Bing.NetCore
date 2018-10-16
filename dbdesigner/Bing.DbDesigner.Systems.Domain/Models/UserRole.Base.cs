using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Tenants;

namespace Bing.DbDesigner.Systems.Domain.Models {
    /// <summary>
    /// 用户角色
    /// </summary>
    [DisplayName( "用户角色" )]
    public partial class UserRole {

        /// <summary>
        /// 用户标识
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// 初始化用户角色
        /// </summary>
        public UserRole() {
        }

        /// <summary>
        /// 初始化用户角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleId">角色标识</param>
        public UserRole(Guid userId, Guid roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }
    }
}