using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 用户登录日志 控制器
    /// </summary>
    public class UserLoginLogController : ApiControllerBase
    {
        /// <summary>
        /// 用户登录日志 服务
        /// </summary>
        public IUserLoginLogService UserLoginLogService { get; }
    
        /// <summary>
        /// 用户登录日志 查询服务
        /// </summary>
        public IQueryUserLoginLogService QueryUserLoginLogService { get; }

        /// <summary>
        /// 初始化一个<see cref="UserLoginLogController"/>类型的实例
        /// </summary>
        /// <param name="service">用户登录日志服务</param>
        /// <param name="queryService">用户登录日志查询服务</param>
        public UserLoginLogController( IUserLoginLogService service, IQueryUserLoginLogService queryService)
        {
            UserLoginLogService = service;
            QueryUserLoginLogService = queryService;
        }
    }
}
