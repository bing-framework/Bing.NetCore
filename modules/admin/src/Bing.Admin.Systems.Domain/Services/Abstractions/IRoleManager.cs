using System;
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
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        Task AddUserToRoleAsync(Guid userId, string roleCode);
    }
}
