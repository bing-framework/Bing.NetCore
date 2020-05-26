using System;
using Bing.Admin.Systems.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// Identity登录管理
    /// </summary>
    public class IdentitySignInManager : Bing.Permissions.Identity.Services.Implements.IdentitySignInManager<User, Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="IdentitySignInManager" />类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户管理器</param>
        /// <param name="contextAccessor">HttpContext访问器</param>
        /// <param name="claimsFactory">用户声明工厂</param>
        /// <param name="optionsAccessor">Identity配置</param>
        /// <param name="logger">日志</param>
        /// <param name="schemes">认证架构提供程序</param>
        public IdentitySignInManager(UserManager<User> userManager
            , IHttpContextAccessor contextAccessor
            , IUserClaimsPrincipalFactory<User> claimsFactory
            , IOptions<IdentityOptions> optionsAccessor
            , ILogger<SignInManager<User>> logger
            , IAuthenticationSchemeProvider schemes)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes)
        {
        }
    }
}
