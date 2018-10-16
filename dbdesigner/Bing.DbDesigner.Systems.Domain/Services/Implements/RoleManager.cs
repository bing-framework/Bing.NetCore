using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.DbDesigner.Systems.Domain.Services.Abstractions;
using Bing.Exceptions;
using Bing.Security.Identity.Repositories;
using Bing.Security.Identity.Services.Implements;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Bing.DbDesigner.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 角色管理
    /// </summary>
    public class RoleManager:RoleManager<Role,Guid,Guid?>,IRoleManager
    {
        /// <summary>
        /// Identity角色管理
        /// </summary>
        protected RoleManager<Role> Manager { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="RoleManager"/>类型的实例
        /// </summary>
        /// <param name="roleManager">Identity角色管理</param>
        /// <param name="repository">角色仓储</param>
        /// <param name="userRepository">用户仓储</param>
        public RoleManager(
            RoleManager<Role> roleManager
            , IRoleRepository repository
            , IUserRepository userRepository) : base(roleManager, repository)
        {
            Manager = roleManager;
            RoleRepository = repository;
            UserRepository = userRepository;
        }

        /// <summary>
        /// 重写角色验证
        /// </summary>
        /// <param name="role">角色</param>
        /// <returns></returns>
        protected override async Task ValidateUpdate(Role role)
        {
            if (await RoleRepository.ExistsAsync(x => x.Id != role.Id && x.Code == role.Code))
            {
                ThrowDuplicateCodeException(role.Code);
            }
        }

        #region AddUsersToRole(添加用户到角色)

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userIds">用户标识列表</param>
        /// <param name="roleId">角色标识</param>
        /// <returns></returns>
        public async Task AddUsersToRole(IList<Guid> userIds, Guid roleId)
        {
            var userRoles = await GetUserRolesAsync(userIds, roleId);
            await RoleRepository.AddUserRolesAsync(userRoles);
        }

        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userIds">用户标识列表</param>
        /// <param name="roleId">角色标识</param>
        /// <returns></returns>
        private async Task<List<UserRole>> GetUserRolesAsync(IList<Guid> userIds, Guid roleId)
        {
            var users = await UserRepository.FindByIdsAsync(userIds);
            if (users.IsEmpty())
            {
                throw new Warning(SystemResource.UsersIsEmpty);
            }

            var role = await RoleRepository.FindAsync(roleId);
            if (role == null)
            {
                throw new Warning(SystemResource.RoleIsEmpty);
            }

            return users.Select(user => new UserRole(user.Id, roleId)).ToList();
        }

        #endregion

        #region RemoveUsersFromRole(从角色移除用户)

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="userIds">用户标识列表</param>
        /// <param name="roleId">角色标识</param>
        /// <returns></returns>
        public async Task RemoveUsersFromRole(IList<Guid> userIds, Guid roleId)
        {
            var userRoles = await GetUserRolesAsync(userIds, roleId);
            RoleRepository.RemoveUserRoles(userRoles);
        }

        #endregion

        #region AddUserToRole(添加用户到角色)

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        public async Task AddUserToRole(Guid userId, string roleCode)
        {
            var role = await RoleRepository.GetByCodeAsync(roleCode);
            if (role == null)
            {
                throw new Warning("角色不存在");
            }

            await RoleRepository.AddUserRolesAsync(new List<UserRole>() {new UserRole(userId, role.Id)});
        }

        #endregion

        #region RemoveUserFromRole(从角色移除用户)

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        public async Task RemoveUserFromRole(Guid userId, string roleCode)
        {
            if (!(await RoleRepository.ExistRoleAsync(userId, roleCode)))
            {
                return;
            }

            var userRole = await GetUserRoleAsync(userId, roleCode);
            RoleRepository.RemoveUserRoles(new List<UserRole>() {userRole});
        }

        /// <summary>
        /// 获取用户角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        private async Task<UserRole> GetUserRoleAsync(Guid userId, string roleCode)
        {
            var user = await UserRepository.FindAsync(userId);
            if (user == null)
            {
                throw new Warning(SystemResource.UsersIsEmpty);
            }

            var role = await RoleRepository.GetByCodeAsync(roleCode);
            if (role == null)
            {
                throw new Warning(SystemResource.RoleIsEmpty);
            }

            return new UserRole(user.Id, role.Id);
        }

        #endregion

        #region ExistRole(是否存在角色)

        /// <summary>
        /// 是否存在角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        public async Task<bool> ExistRole(Guid userId, string roleCode)
        {
            return await RoleRepository.ExistRoleAsync(userId, roleCode);
        }

        #endregion

    }
}
