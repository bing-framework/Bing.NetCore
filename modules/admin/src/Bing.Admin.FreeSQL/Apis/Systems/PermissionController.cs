using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.AspNetCore.Mvc;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 权限 控制器
    /// </summary>
    public class PermissionController : ApiControllerBase
    {
        /// <summary>
        /// 权限 服务
        /// </summary>
        public IPermissionService PermissionService { get; }
    
        /// <summary>
        /// 权限 查询服务
        /// </summary>
        public IQueryPermissionService QueryPermissionService { get; }

        /// <summary>
        /// 初始化一个<see cref="PermissionController"/>类型的实例
        /// </summary>
        /// <param name="service">权限服务</param>
        /// <param name="queryService">权限查询服务</param>
        public PermissionController( IPermissionService service, IQueryPermissionService queryService)
        {
            PermissionService = service;
            QueryPermissionService = queryService;
        }

        /// <summary>
        /// 获取资源标识列表
        /// </summary>
        /// <param name="query">查询参数</param>
        [HttpGet("resourceIds")]
        public async Task<IActionResult> GetResourceIdsAsync([FromQuery] PermissionQuery query)
        {
            var result = await PermissionService.GetResourceIdsAsync(query);
            return Success(result);
        }

        /// <summary>
        /// 保存权限
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> SaveAsync([FromBody] SavePermissionRequest request)
        {
            await PermissionService.SaveAsync(request);
            return Success();
        }

        /// <summary>
        /// 获取超级管理员角色菜单列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        [HttpGet("getAllMenus")]
        [ProducesResponseType(typeof(List<SelectModuleResponse>), 200)]
        public async Task<IActionResult> GetAllMenusAsync(Guid applicationId, Guid roleId)
        {
            var result = await QueryPermissionService.GetAllMenusAsync(applicationId, roleId);
            return Success(result);
        }

        /// <summary>
        /// 获取商户下角色菜单列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        [HttpGet("getRoleMenus")]
        [ProducesResponseType(typeof(List<SelectModuleResponse>), 200)]
        public async Task<IActionResult> GetRoleMenusAsync(Guid applicationId, Guid roleId)
        {
            var result = await QueryPermissionService.GetRoleMenusAsync(applicationId, roleId);
            return Success(result);
        }
    }
}
