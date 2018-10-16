using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Services.Abstractions;

namespace Bing.DbDesigner.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 角色管理
    /// </summary>
    public interface IRoleManager:IRoleManager<Role,Guid,Guid?>
    {
        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userIds">用户标识列表</param>
        /// <param name="roleId">角色标识</param>
        /// <returns></returns>
        Task AddUsersToRole(IList<Guid> userIds, Guid roleId);

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="userIds">用户标识列表</param>
        /// <param name="roleId">角色标识</param>
        /// <returns></returns>
        Task RemoveUsersFromRole(IList<Guid> userIds, Guid roleId);

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        Task AddUserToRole(Guid userId, string roleCode);

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        Task RemoveUserFromRole(Guid userId, string roleCode);

        /// <summary>
        /// 是否存在角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        Task<bool> ExistRole(Guid userId, string roleCode);
    }
}
