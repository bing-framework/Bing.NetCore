using System.Threading.Tasks;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.Requests.Systems;
using Bing.AspNetCore.Mvc;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Permissions.Identity.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis
{
    /// <summary>
    /// 授权控制器
    /// </summary>
    public class OAuthController : ApiControllerBase
    {
        /// <summary>
        /// 初始化一个<see cref="OAuthController"/>类型的实例
        /// </summary>
        public OAuthController(ISecurityService securityService)
        {
            SecurityService = securityService;
        }

        /// <summary>
        /// 安全服务
        /// </summary>
        protected ISecurityService SecurityService { get; }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="request">请求</param>
        [AllowAnonymous]
        [HttpPost("signIn")]
        [ProducesResponseType(typeof(JsonWebToken), 200)]
        public async Task<IActionResult> SignInAsync([FromBody] AdminLoginRequest request)
        {
            var result = await SecurityService.SignInAsync(request);
            if (result.State == SignInState.Succeeded)
                return Success(result.Token);
            return Fail(result.Message);
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        [AllowAnonymous]
        [HttpPost("refreshToken")]
        [ProducesResponseType(typeof(JsonWebToken), 200)]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] string token)
        {
            var result = await SecurityService.RefreshTokenAsync(token);
            return Success(result);
        }
    }
}
