using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Jwt.Controllers
{
    public class OAuthController : ApiControllerBase
    {
        /// <summary>
        /// Jwt令牌构建器
        /// </summary>
        public IJsonWebTokenBuilder TokenBuilder { get; }

        /// <summary>
        /// 初始化一个<see cref="OAuthController"/>类型的实例
        /// </summary>
        /// <param name="tokenBuilder">Jwt令牌构建器</param>
        public OAuthController(IJsonWebTokenBuilder tokenBuilder)
        {
            TokenBuilder = tokenBuilder;
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="request">请求</param>
        [AllowAnonymous]
        [HttpPost("signIn")]
        public async Task<IActionResult> SignInAsync([FromBody] LoginRequest request)
        {
            var payload = new Dictionary<string, string>();
            payload["clientId"] = "66666";
            payload["userId"] = Guid.NewGuid().ToString();
            payload["username"] = request.UserName;
            var result = await TokenBuilder.CreateAsync(payload);
            return Success(result);
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] string token)
        {
            return Success(await TokenBuilder.RefreshAsync(token));
        }

        /// <summary>
        /// 获取内容
        /// </summary>
        /// <param name="content">内容</param>
        [HttpGet]
        public Task<IActionResult> GetAsync(string content)
        {
            return Task.FromResult(Success(content));
        }
    }

    public class LoginRequest
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
