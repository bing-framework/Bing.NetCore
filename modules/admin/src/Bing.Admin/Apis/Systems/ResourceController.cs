using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 资源 控制器
    /// </summary>
    public class ResourceController : ApiControllerBase
    {
        /// <summary>
        /// 资源 服务
        /// </summary>
        public IResourceService ResourceService { get; }
    
        /// <summary>
        /// 资源 查询服务
        /// </summary>
        public IQueryResourceService QueryResourceService { get; }

        /// <summary>
        /// 初始化一个<see cref="ResourceController"/>类型的实例
        /// </summary>
        /// <param name="service">资源服务</param>
        /// <param name="queryService">资源查询服务</param>
        public ResourceController( IResourceService service, IQueryResourceService queryService)
        {
            ResourceService = service;
            QueryResourceService = queryService;
        }
    }
}
