using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

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
    }
}
