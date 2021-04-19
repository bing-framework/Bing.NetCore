using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Domain.Shared;
using Bing.AspNetCore.Mvc;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Users;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 角色 控制器
    /// </summary>
    public class RoleController : QueryControllerBase<RoleDto, RoleQuery>
    {
        /// <summary>
        /// 角色 服务
        /// </summary>
        public IRoleService RoleService { get; }

        /// <summary>
        /// 角色 查询服务
        /// </summary>
        public IQueryRoleService QueryRoleService { get; }

        /// <summary>
        /// 初始化一个<see cref="RoleController"/>类型的实例
        /// </summary>
        /// <param name="service">角色服务</param>
        /// <param name="queryService">角色查询服务</param>
        public RoleController(IRoleService service, IQueryRoleService queryService) : base(service)
        {
            RoleService = service;
            QueryRoleService = queryService;
        }

        /// <summary>
        /// 创建系统角色
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRoleRequest request)
        {
            request.Type = RoleTypeCode.SystemRole;
            request.IsSystem = true;
            var id = await RoleService.CreateAsync(request);
            return Success(id);
        }

        /// <summary>
        /// 创建租户角色
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost("createTenantRole")]
        public async Task<IActionResult> CreateTenantAsync([FromBody] CreateRoleRequest request)
        {
            request.Type = RoleTypeCode.TenantRole;
            request.IsSystem = false;
            request.TenantId = CurrentUser.GetTenantCode();
            var id = await RoleService.CreateAsync(request);
            return Success(id);
        }

        /// <summary>
        /// 修改角色
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateRoleRequest request)
        {
            await RoleService.UpdateAsync(request);
            return Success();
        }

        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="ids">标识列表。多个Id用逗号分隔。范例：1,2,3</param>
        /// <returns></returns>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteAsync([FromBody] string ids)
        {
            await RoleService.DeleteAsync(ids);
            return Success();
        }

        /// <summary>
        /// 添加用户列表到角色
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost("addUsersToRole")]
        public async Task<IActionResult> AddUsersToRoleAsync([FromBody] UserRoleRequest request)
        {
            await RoleService.AddUsersToRoleAsync(request);
            return Success();
        }

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost("removeUsersFromRole")]
        public async Task<IActionResult> RemoveUsersFromRoleAsync([FromBody] UserRoleRequest request)
        {
            await RoleService.RemoveUsersFromRoleAsync(request);
            return Success();
        }

        /// <summary>
        /// 获取系统角色列表
        /// </summary>
        [ProducesResponseType(typeof(List<RoleResponse>), 200)]
        [HttpGet("getSystemRoleList")]
        public async Task<IActionResult> GetSystemRoleListAsync()
        {
            var result = await RoleService.GetListAsync(RoleTypeCode.SystemRole, null);
            return Success(result);
        }

        /// <summary>
        /// 获取租户角色列表
        /// </summary>
        [ProducesResponseType(typeof(List<RoleResponse>), 200)]
        [HttpGet("getTenantRoleList")]
        public async Task<IActionResult> GetTenantRoleListAsync()
        {
            var tenantId = CurrentUser.GetTenantCode();
            var result = await RoleService.GetListAsync(RoleTypeCode.TenantRole, tenantId);
            return Success(result);
        }
    }
}
