using System;
using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Permissions.Identity.Options;
using Bing.Permissions.Identity.Services.Implements;
using Microsoft.Extensions.Options;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户 管理
    /// </summary>
    public class UserManager : Bing.Permissions.Identity.Services.Implements.UserManager<User, Guid>, IUserManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserManager" />类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户管理</param>
        /// <param name="options">权限配置</param>
        /// <param name="userRepository">用户仓储</param>
        public UserManager(IdentityUserManager userManager
            , IOptions<PermissionOptions> options
            , IUserRepository userRepository)
            : base(userManager, options, userRepository)
        {
        }
    }
}
