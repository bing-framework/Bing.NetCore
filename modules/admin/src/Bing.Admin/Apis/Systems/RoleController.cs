using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 角色 控制器
    /// </summary>
    public class RoleController : ApiControllerBase
    {
        /// <summary>
        /// 角色 服务
        /// </summary>
        public IRoleService RoleService { get; }
    
        /// <summary>
        /// 角色 查询服务
        /// </summary>
        public IQueryRoleService QueryRoleService { get; }

        /// <summary>
        /// 初始化一个<see cref="RoleController"/>类型的实例
        /// </summary>
        /// <param name="service">角色服务</param>
        /// <param name="queryService">角色查询服务</param>
        public RoleController( IRoleService service, IQueryRoleService queryService)
        {
            RoleService = service;
            QueryRoleService = queryService;
        }
    }
}
