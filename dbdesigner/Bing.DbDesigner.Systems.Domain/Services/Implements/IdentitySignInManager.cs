using System;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Services.Implements;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.DbDesigner.Systems.Domain.Services.Implements
{
    /// <summary>
    /// Identity登录管理
    /// </summary>
    public class IdentitySignInManager: IdentitySignInManager<User,Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="IdentitySignInManager"/>类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户服务</param>
        /// <param name="contextAccessor">HttpContext访问器</param>
        /// <param name="claimsFactory">用户声明工厂</param>
        /// <param name="optionsAccessor">Identity配置</param>
        /// <param name="logger">日志</param>
        /// <param name="schemes">认证架构提供程序</param>
        public IdentitySignInManager(
            UserManager<User> userManager
            , IHttpContextAccessor contextAccessor
            , IUserClaimsPrincipalFactory<User> claimsFactory
            , IOptions<IdentityOptions> optionsAccessor
            , ILogger<SignInManager<User>> logger
            , IAuthenticationSchemeProvider schemes) : base(userManager, contextAccessor, claimsFactory,
            optionsAccessor, logger, schemes)
        {
        }
    }
}
