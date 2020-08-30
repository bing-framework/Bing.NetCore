using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 角色管理
    /// </summary>
    public interface IRoleManager : Bing.Permissions.Identity.Services.Abstractions.IRoleManager<Role, Guid, Guid?>
    {
        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        Task AddUsersToRoleAsync(Guid roleId, List<Guid> userIds);

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        Task RemoveUsersFromRoleAsync(Guid roleId, List<Guid> userIds);

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        Task AddUserToRoleAsync(Guid userId, string roleCode);

        /// <summary>
        /// 添加角色到用户
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task AddRolesToUserAsync(Guid userId, List<Guid> roleIds);

        /// <summary>
        /// 从用户移除角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task RemoveRolesFromUserAsync(Guid userId, List<Guid> roleIds);

        /// <summary>
        /// 更新用户角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task UpdateUserRoleAsync(Guid userId, List<Guid> roleIds);

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">角色标识列表</param>
        Task DeleteAsync(List<Guid> ids);
    }
}
