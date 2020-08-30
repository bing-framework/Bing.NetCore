using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 角色仓储
    /// </summary>
    public interface IRoleRepository : Bing.Permissions.Identity.Repositories.IRoleRepository<Role, Guid, Guid?>
    {
        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        Task<List<Role>> GetRolesAsync(Guid userId);

        /// <summary>
        /// 获取用户的角色标识列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        Task<List<Guid>> GetRoleIdsAsync(Guid userId);

        /// <summary>
        /// 获取角色的用户标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        Task<List<Guid>> GetUserIdsAsync(Guid roleId);

        /// <summary>
        /// 获取已添加的用户标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        Task<List<Guid>> GetExistsUserIdsAsync(Guid roleId, List<Guid> userIds);

        /// <summary>
        /// 添加用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        Task AddUserRolesAsync(IEnumerable<UserRole> userRoles);

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        void RemoveUserRoles(IEnumerable<UserRole> userRoles);

        /// <summary>
        /// 通过编码获取角色
        /// </summary>
        /// <param name="code">编码</param>
        Task<Role> GetByCodeAsync(string code);
    }
}
