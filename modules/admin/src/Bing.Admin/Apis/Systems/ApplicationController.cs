using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 应用程序 控制器
    /// </summary>
    public class ApplicationController : ApiControllerBase
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
        public ApplicationController( IApplicationService service, IQueryApplicationService queryService)
        {
            ApplicationService = service;
            QueryApplicationService = queryService;
        }
    }
}
