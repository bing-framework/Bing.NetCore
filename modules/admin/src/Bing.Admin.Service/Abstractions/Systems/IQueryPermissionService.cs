using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Responses.Systems;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 权限 查询服务
    /// </summary>
    public interface IQueryPermissionService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        Task<List<SelectModuleResponse>> GetAllMenusAsync(Guid applicationId, Guid roleId);

        /// <summary>
        /// 获取角色菜单
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        Task<List<SelectModuleResponse>> GetRoleMenusAsync(Guid applicationId, Guid roleId);
    }
}
