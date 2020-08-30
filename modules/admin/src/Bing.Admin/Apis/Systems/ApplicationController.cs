using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.AspNetCore.Mvc;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 应用程序 控制器
    /// </summary>
    public class ApplicationController : QueryControllerBase<ApplicationDto, ApplicationQuery>
    {
        /// <summary>
        /// 应用程序 服务
        /// </summary>
        public IApplicationService ApplicationService { get; }

        /// <summary>
        /// 应用程序 查询服务
        /// </summary>
        public IQueryApplicationService QueryApplicationService { get; }

        /// <summary>
        /// 初始化一个<see cref="ApplicationController"/>类型的实例
        /// </summary>
        /// <param name="service">应用程序服务</param>
        /// <param name="queryService">应用程序查询服务</param>
        public ApplicationController(IApplicationService service, IQueryApplicationService queryService) : base(queryService)
        {
            ApplicationService = service;
            QueryApplicationService = queryService;
        }

        /// <summary>
        /// 获取全部应用程序
        /// </summary>
        [ProducesResponseType(typeof(List<ApplicationDto>), 200)]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await QueryApplicationService.GetAllAsync();
            return Success(result);
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateApplicationRequest request)
        {
            var id = await ApplicationService.CreateAsync(request);
            return Success(id);
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateApplicationRequest request)
        {
            await ApplicationService.UpdateAsync(request);
            return Success();
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">标识列表，多个Id用逗号分隔。范例：1,2,3</param>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteAsync([FromBody] string ids)
        {
            await ApplicationService.DeleteAsync(ids);
            return Success();
        }
    }
}
