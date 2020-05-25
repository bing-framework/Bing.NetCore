using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Apis.Commons
{
    /// <summary>
    /// 地区 控制器
    /// </summary>
    public class AreaController : ApiControllerBase
    {
        /// <summary>
        /// 地区 服务
        /// </summary>
        public IAreaService AreaService { get; }
    
        /// <summary>
        /// 地区 查询服务
        /// </summary>
        public IQueryAreaService QueryAreaService { get; }

        /// <summary>
        /// 初始化一个<see cref="AreaController"/>类型的实例
        /// </summary>
        /// <param name="service">地区服务</param>
        /// <param name="queryService">地区查询服务</param>
        public AreaController( IAreaService service, IQueryAreaService queryService)
        {
            AreaService = service;
            QueryAreaService = queryService;
        }
    }
}
