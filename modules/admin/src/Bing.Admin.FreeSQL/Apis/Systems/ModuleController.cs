using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 模块 控制器
    /// </summary>
    public class ModuleController : ApiControllerBase
    {
        /// <summary>
        /// 初始化一个<see cref="ModuleController" />类型的实例
        /// </summary>
        /// <param name="service">模块服务</param>
        /// <param name="queryService">模块查询服务</param>
        public ModuleController(IModuleService service
            , IQueryModuleService queryService)
        {
            Service = service;
            QueryService = queryService;
        }

        /// <summary>
        /// 模块服务
        /// </summary>
        public IModuleService Service { get; }

        /// <summary>
        /// 模块查询服务
        /// </summary>
        public IQueryModuleService QueryService { get; }

        /// <summary>
        /// 创建模块
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateModuleRequest request)
        {
            var id = await Service.CreateAsync(request);
            return Success(id);
        }

        /// <summary>
        /// 修改模块
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateModuleRequest request)
        {
            await Service.UpdateAsync(request);
            return Success();
        }

        /// <summary>
        /// 获取模块树型列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        [HttpGet("getModuleTree")]
        [ProducesResponseType(typeof(List<ModuleResponse>), 200)]
        public async Task<IActionResult> GetModuleTreeAsync([FromQuery] Guid applicationId)
        {
            var result = await QueryService.GetModuleTreeAsync(applicationId);
            return Success(result);
        }

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        [HttpGet("getChildren")]
        [ProducesResponseType(typeof(List<ModuleResponse>), 200)]
        public async Task<IActionResult> GetChildrenAsync([FromQuery][Required] Guid applicationId, [FromQuery] Guid? moduleId)
        {
            var result = await QueryService.GetChildrenAsync(applicationId, moduleId);
            return Success(result);
        }

        /// <summary>
        /// 删除模块
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteAsync([FromBody] string ids)
        {
            await Service.DeleteAsync(ids);
            return Success();
        }
    }
}
