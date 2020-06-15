using System.Threading.Tasks;
using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Requests.Systems;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Systems
{
    /// <summary>
    /// 用户 控制器
    /// </summary>
    public class UserController : ApiControllerBase
    {
        /// <summary>
        /// 用户 服务
        /// </summary>
        public IUserService UserService { get; }
    
        /// <summary>
        /// 用户 查询服务
        /// </summary>
        public IQueryUserService QueryUserService { get; }

        /// <summary>
        /// 初始化一个<see cref="UserController"/>类型的实例
        /// </summary>
        /// <param name="service">用户服务</param>
        /// <param name="queryService">用户查询服务</param>
        public UserController( IUserService service, IQueryUserService queryService)
        {
            UserService = service;
            QueryUserService = queryService;
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] UpdatePasswordRequest request)
        {
            await UserService.ChangePasswordAsync(null, request.OldPassword, request.NewPassword);
            return Success();
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request)
        {
            await UserService.ResetPasswordAsync(request);
            return Success();
        }
    }
}
