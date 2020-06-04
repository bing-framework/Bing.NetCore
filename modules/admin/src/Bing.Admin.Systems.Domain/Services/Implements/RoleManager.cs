using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Exceptions;
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
        public RoleManager(RoleManager<Role> roleManager, IRoleRepository roleRepository) : base(roleManager,
            roleRepository)
        {
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; }

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        public async Task AddUserToRoleAsync(Guid userId, string roleCode)
        {
            var role = await RoleRepository.GetByCodeAsync(roleCode);
            if (role == null)
                throw new Warning("角色不存在");
            await RoleRepository.AddUserRolesAsync(new List<UserRole> { new UserRole { UserId = userId, RoleId = role.Id } });
        }
    }
}
