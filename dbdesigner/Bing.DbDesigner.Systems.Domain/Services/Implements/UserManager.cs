using System;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.DbDesigner.Systems.Domain.Services.Abstractions;
using Bing.Security.Identity.Options;
using Bing.Security.Identity.Repositories;
using Bing.Security.Identity.Services.Implements;
using Microsoft.Extensions.Options;

namespace Bing.DbDesigner.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户管理
    /// </summary>
    public class UserManager:UserManager<User,Guid>,IUserManager
    {
        /// <summary>
        /// Identity用户管理
        /// </summary>
        protected IdentityUserManager Manager { get; set; }

        /// <summary>
        /// 初始化一个<see cref="UserManager"/>类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户管理</param>
        /// <param name="options">权限配置</param>
        /// <param name="userRepository">用户仓储</param>
        public UserManager(
            IdentityUserManager userManager
            , IOptions<PermissionOptions> options
            , IUserRepository userRepository) : base(userManager, options, userRepository)
        {
            Manager = userManager;
        }
    }
}
