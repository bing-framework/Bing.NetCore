using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Exceptions;
using Bing.Extensions;
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
        public RoleManager(RoleManager<Role> roleManager
            , IRoleRepository roleRepository)
            : base(roleManager, roleRepository)
        {
            Manager = roleManager;
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// Identity角色管理
        /// </summary>
        protected RoleManager<Role> Manager { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; }

        /// <summary>
        /// 创建角色验证
        /// </summary>
        /// <param name="role">角色</param>
        protected override async Task ValidateCreate(Role role)
        {
            role.CheckNotNull(nameof(role));
            if (await RoleRepository.ExistsAsync(t => t.Code == role.Code))
                ThrowDuplicateCodeException(role.Code);
            if (await RoleRepository.ExistsAsync(t => t.Name == role.Name))
                ThrowDuplicateNameException(role.Name);
        }

        /// <summary>
        /// 修改角色验证
        /// </summary>
        /// <param name="role">角色</param>
        protected override async Task ValidateUpdate(Role role)
        {
            role.CheckNotNull(nameof(role));
            if (await RoleRepository.ExistsAsync(t => t.Code == role.Code && t.Id != role.Id))
                ThrowDuplicateCodeException(role.Code);
            if (await RoleRepository.ExistsAsync(t => t.Name == role.Name && t.Id != role.Id))
                ThrowDuplicateNameException(role.Name);
        }

        /// <summary>
        /// 添加多个用户到指定角色
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        public async Task AddUsersToRoleAsync(Guid roleId, List<Guid> userIds)
        {
            if (roleId.IsEmpty() || userIds == null)
                return;
            var existsUserIds = await RoleRepository.GetExistsUserIdsAsync(roleId, userIds);
            userIds = userIds.ToList().Except(existsUserIds).ToList();
            var userRoles = CreateUserRoles(roleId, userIds);
            await RoleRepository.AddUserRolesAsync(userRoles);
        }

        /// <summary>
        /// 创建用户角色列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        private List<UserRole> CreateUserRoles(Guid roleId, List<Guid> userIds) => userIds
            .Where(x => x.IsEmpty() == false)
            .Select(userId => new UserRole(userId, roleId))
            .ToList();

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        public Task RemoveUsersFromRoleAsync(Guid roleId, List<Guid> userIds)
        {
            if (roleId.IsEmpty() || userIds == null)
                return Task.CompletedTask;
            var userRoles = CreateUserRoles(roleId, userIds);
            RoleRepository.RemoveUserRoles(userRoles);
            return Task.CompletedTask;
        }

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

        /// <summary>
        /// 添加角色到用户
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task AddRolesToUserAsync(Guid userId, List<Guid> roleIds)
        {
            if (userId.IsEmpty() || roleIds == null)
                return;
            var existsRoleIds = await RoleRepository.GetRoleIdsAsync(userId);
            roleIds = existsRoleIds.ToList().Except(roleIds).ToList();
            var userRoles = CreateUserRolesByUser(userId, roleIds);
            await RoleRepository.AddUserRolesAsync(userRoles);
        }

        /// <summary>
        /// 创建用户角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        private List<UserRole> CreateUserRolesByUser(Guid userId, List<Guid> roleIds) => roleIds
            .Where(x => x.IsEmpty() == false)
            .Select(roleId => new UserRole(userId, roleId))
            .ToList();

        /// <summary>
        /// 从用户移除角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public Task RemoveRolesFromUserAsync(Guid userId, List<Guid> roleIds)
        {
            if (userId.IsEmpty() || roleIds == null)
                return Task.CompletedTask;
            var userRoles = CreateUserRolesByUser(userId, roleIds);
            RoleRepository.RemoveUserRoles(userRoles);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新用户角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task UpdateUserRoleAsync(Guid userId, List<Guid> roleIds)
        {
            if (userId.IsEmpty() || roleIds == null)
                return;
            var existsRoleIds = await RoleRepository.GetRoleIdsAsync(userId);
            var exceptRoleIds = !existsRoleIds.Any() ? roleIds : roleIds.Except(existsRoleIds);// 差值后，可能存在新增或移除的角色
            var removeRoleIds = new List<Guid>();
            foreach (var roleId in existsRoleIds)
            {
                if (roleIds.All(x => x != roleId))
                    removeRoleIds.Add(roleId);
            }

            var addRoleIds = exceptRoleIds.Except(removeRoleIds).ToList();
            var addUserRoles = CreateUserRolesByUser(userId, addRoleIds);
            var removeUserRoles = CreateUserRolesByUser(userId, removeRoleIds);
            await RoleRepository.AddUserRolesAsync(addUserRoles);
            RoleRepository.RemoveUserRoles(removeUserRoles);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">角色标识列表</param>
        public async Task DeleteAsync(List<Guid> ids)
        {
            var entities = await RoleRepository.FindByIdsAsync(ids);
            await RoleRepository.RemoveAsync(entities);
        }
    }
}
