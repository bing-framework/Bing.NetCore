using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

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
    }
}
