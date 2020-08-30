using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.AspNetCore.Mvc;
using Bing.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 操作 控制器
    /// </summary>
    public class OperationController:ApiControllerBase
    {
        /// <summary>
        /// 初始化一个<see cref="OperationController"/>类型的实例
        /// </summary>
        /// <param name="service">操作服务</param>
        /// <param name="queryService">操作查询服务</param>
        public OperationController(IOperationService service, IQueryOperationService queryService)
        {
            Service = service;
            QueryService = queryService;
        }

        /// <summary>
        /// 操作服务
        /// </summary>
        public IOperationService Service { get; }

        /// <summary>
        /// 操作查询服务
        /// </summary>
        public IQueryOperationService QueryService { get; set; }

        /// <summary>
        /// 创建操作
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateOperationRequest request)
        {
            var id = await Service.CreateAsync(request);
            return Success(id);
        }

        /// <summary>
        /// 修改操作
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateOperationRequest request)
        {
            await Service.UpdateAsync(request);
            return Success();
        }

        /// <summary>
        /// 删除操作
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteAsync([FromBody] string ids)
        {
            await Service.DeleteAsync(ids);
            return Success();
        }

        /// <summary>
        /// 获取操作
        /// </summary>
        /// <param name="id">操作标识</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(OperationDto), 200)]
        public async Task<IActionResult> GetAsync(string id)
        {
            var result = await QueryService.GetByIdAsync(id.ToGuid());
            return Success(result);
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        [HttpGet("getOperations")]
        [ProducesResponseType(typeof(List<OperationDto>), 200)]
        public async Task<IActionResult> GetOperationsAsync(string moduleId)
        {
            var result = await QueryService.GetOperationsAsync(moduleId.ToGuid());
            return Success(result);
        }
    }
}
