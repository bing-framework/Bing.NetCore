using System;
using System.Threading.Tasks;
using Bing.AspNetCore.Mvc;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Data;
using Bing.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 管理员 控制器
    /// </summary>
    public class AdministratorController : ApiControllerBase
    {
        /// <summary>
        /// 管理员 服务
        /// </summary>
        public IAdministratorService AdministratorService { get; }
    
        /// <summary>
        /// 管理员 查询服务
        /// </summary>
        public IQueryAdministratorService QueryAdministratorService { get; }

        /// <summary>
        /// 初始化一个<see cref="AdministratorController"/>类型的实例
        /// </summary>
        /// <param name="service">管理员服务</param>
        /// <param name="queryService">管理员查询服务</param>
        public AdministratorController( IAdministratorService service, IQueryAdministratorService queryService)
        {
            AdministratorService = service;
            QueryAdministratorService = queryService;
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] AdministratorCreateRequest request)
        {
            var result = await AdministratorService.CreateAsync(request);
            return Success(result);
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody]AdministratorUpdateRequest request)
        {
            await AdministratorService.UpdateAsync(request);
            return Success();
        }

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        [HttpPost("enable")]
        public async Task<IActionResult> EnableAsync([FromBody]string ids)
        {
            await AdministratorService.EnableAsync(ids.ToGuidList());
            return Success();
        }

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        [HttpPost("disable")]
        public async Task<IActionResult> DisableAsync([FromBody]string ids)
        {
            await AdministratorService.DisableAsync(ids.ToGuidList());
            return Success();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <remarks>
        /// 调用范例：
        /// GET
        /// /api/customers?name=a
        /// </remarks>
        /// <param name="query">查询参数</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagerList<AdministratorResponse>), 200)]
        public async Task<IActionResult> PagerQueryAsync([FromQuery] AdministratorQuery query)
        {
            var result = await QueryAdministratorService.PagerQueryAsync(query);
            return Success(result);
        }

        /// <summary>
        /// 获取单个实例
        /// </summary>
        /// <remarks>
        /// 调用范例：
        /// GET
        /// /api/customers/1
        /// </remarks>
        /// <param name="id">标识</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AdministratorResponse), 200)]
        public async Task<IActionResult> GetAsync(Guid id)
        {
            var result = await QueryAdministratorService.GetById(id);
            return Success(result);
        }
    }
}
