using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Application.Services;
using Bing.Aspects;
using Bing.Validation;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 角色 服务
    /// </summary>
    public interface IRoleService : IDeleteAppService<RoleDto, RoleQuery>
    {
        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        Task<List<RoleDto>> GetRolesAsync(Guid userId);

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="request">创建角色请求</param>
        Task<Guid> CreateAsync([NotNull][Valid] CreateRoleRequest request);

        /// <summary>
        /// 修改角色
        /// </summary>
        /// <param name="request">修改角色请求</param>
        Task UpdateAsync([NotNull][Valid] UpdateRoleRequest request);

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="request">用户角色请求</param>
        Task AddUsersToRoleAsync(UserRoleRequest request);

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="request">用户角色请求</param>
        Task RemoveUsersFromRoleAsync(UserRoleRequest request);

        /// <summary>
        /// 获取角色列表
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="tenantId">租户标识</param>
        Task<List<RoleResponse>> GetListAsync(string type, string tenantId);
    }
}
