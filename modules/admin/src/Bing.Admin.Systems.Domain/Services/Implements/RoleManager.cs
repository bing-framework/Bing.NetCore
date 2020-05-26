using System;
using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 角色 管理
    /// </summary>
    public class RoleManager : Bing.Permissions.Identity.Services.Implements.RoleManager<Role, Guid, Guid?>, IRoleManager
    {
        /// <summary>
        /// 初始化一个<see cref="RoleManager"/>类型的实例
        /// </summary>
        public RoleManager(RoleManager<Role> roleManager, IRoleRepository roleRepository) : base(roleManager, roleRepository) { }
    }
}
