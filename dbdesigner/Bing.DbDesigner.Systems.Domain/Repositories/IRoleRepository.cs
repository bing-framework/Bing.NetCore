using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Repositories;

namespace Bing.DbDesigner.Systems.Domain.Repositories
{
    /// <summary>
    /// 角色仓储
    /// </summary>
    public interface IRoleRepository :IRoleRepository<Role, Guid, Guid?>
    {
        /// <summary>
        /// 添加用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        /// <returns></returns>
        Task AddUserRolesAsync(IEnumerable<UserRole> userRoles);

        /// <summary>
        /// 移除用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        void RemoveUserRoles(IEnumerable<UserRole> userRoles);

        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <returns></returns>
        Task<List<Role>> GetRolesAsync(Guid userId);

        /// <summary>
        /// 通过角色编码查找
        /// </summary>
        /// <param name="code">角色编码</param>
        /// <returns></returns>
        Task<Role> GetByCodeAsync(string code);

        /// <summary>
        /// 是否存在角色
        /// </summary>
        /// <param name="user">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        Task<bool> ExistRoleAsync(Guid user, string roleCode);
    }
}