using Bing.Admin.Service.Abstractions.Commons;
using Bing.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Commons
{
    /// <summary>
    /// 用户信息 控制器
    /// </summary>
    public class UserInfoController : ApiControllerBase
    {
        /// <summary>
        /// 用户信息 服务
        /// </summary>
        public IUserInfoService UserInfoService { get; }
    
        /// <summary>
        /// 用户信息 查询服务
        /// </summary>
        public IQueryUserInfoService QueryUserInfoService { get; }

        /// <summary>
        /// 初始化一个<see cref="UserInfoController"/>类型的实例
        /// </summary>
        /// <param name="service">用户信息服务</param>
        /// <param name="queryService">用户信息查询服务</param>
        public UserInfoController( IUserInfoService service, IQueryUserInfoService queryService)
        {
            UserInfoService = service;
            QueryUserInfoService = queryService;
        }
    }
}
