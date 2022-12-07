﻿using System.Threading.Tasks;
using Bing.Permissions.Identity.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Permissions.Identity.Services.Implements;

/// <summary>
/// Identity登录服务
/// </summary>
/// <typeparam name="TUser">用户类型</typeparam>
/// <typeparam name="TKey">用户标识类型</typeparam>
public class IdentitySignInManager<TUser, TKey> : SignInManager<TUser> where TUser : UserBase<TUser, TKey>
{
#if NETCOREAPP3_1
        /// <summary>
        /// 初始化一个<see cref="IdentitySignInManager{TUser,TKey}"/>类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户管理器</param>
        /// <param name="contextAccessor">HttpContext访问器</param>
        /// <param name="claimsFactory">用户声明工厂</param>
        /// <param name="optionsAccessor">Identity配置</param>
        /// <param name="logger">日志</param>
        /// <param name="schemes">认证架构提供程序</param>
        /// <param name="confirmation">用户确认</param>
        public IdentitySignInManager(UserManager<TUser> userManager
            , IHttpContextAccessor contextAccessor
            , IUserClaimsPrincipalFactory<TUser> claimsFactory
            , IOptions<IdentityOptions> optionsAccessor
            , ILogger<SignInManager<TUser>> logger
            , IAuthenticationSchemeProvider schemes
            , IUserConfirmation<TUser> confirmation)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
        }
#else
    /// <summary>
    /// 初始化一个<see cref="IdentitySignInManager{TUser,TKey}"/>类型的实例
    /// </summary>
    /// <param name="userManager">Identity用户管理器</param>
    /// <param name="contextAccessor">HttpContext访问器</param>
    /// <param name="claimsFactory">用户声明工厂</param>
    /// <param name="optionsAccessor">Identity配置</param>
    /// <param name="logger">日志</param>
    /// <param name="schemes">认证架构提供程序</param>
    public IdentitySignInManager(UserManager<TUser> userManager
        , IHttpContextAccessor contextAccessor
        , IUserClaimsPrincipalFactory<TUser> claimsFactory
        , IOptions<IdentityOptions> optionsAccessor
        , ILogger<SignInManager<TUser>> logger
        , IAuthenticationSchemeProvider schemes)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes)
    {
    }
#endif

    /// <summary>
    /// 是否允许登录
    /// </summary>
    /// <param name="user">用户</param>
    public override async Task<bool> CanSignInAsync(TUser user)
    {
        if (user.Enabled == false)
            return false;
        return await base.CanSignInAsync(user);
    }
}