using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Bing.Admin.Apis
{
    /// <summary>
    /// 测试查询控制器
    /// </summary>
    [AllowAnonymous]
    public class TestQueryController : QueryControllerBase<AdministratorResponse, AdministratorQuery>
    {
        /// <summary>
        /// 初始化一个<see cref="QueryControllerBase{TDto,TQuery}"/>类型的实例
        /// </summary>
        /// <param name="service">查询服务</param>
        public TestQueryController(ITestQueryService service) : base(service)
        {
        }
    }
}
