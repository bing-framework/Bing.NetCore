using System;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Permissions.Identity.Services.Implements;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 登录管理
    /// </summary>
    public class SignInManager : SignInManager<User, Guid>, ISignInManager
    {
        /// <summary>
        /// 初始化一个<see cref="SignInManager"/>类型的实例
        /// </summary>
        /// <param name="identitySignInManager">Identity登录管理</param>
        /// <param name="userManager">用户管理</param>
        public SignInManager(IdentitySignInManager identitySignInManager
            , IUserManager userManager)
            : base(identitySignInManager, userManager)
        {
        }
    }
}
