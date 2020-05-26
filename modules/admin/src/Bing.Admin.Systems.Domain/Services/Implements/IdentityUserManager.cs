using System;
using System.Collections.Generic;
using Bing.Admin.Systems.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// Identity用户管理
    /// </summary>
    public class IdentityUserManager : Bing.Permissions.Identity.Services.Implements.IdentityUserManager<User, Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="IdentityUserManager" />类型的实例
        /// </summary>
        /// <param name="store">用户存储</param>
        /// <param name="optionsAccessor">配置</param>
        /// <param name="passwordHasher">密码加密器</param>
        /// <param name="userValidators">用户验证器</param>
        /// <param name="passwordValidators">密码验证器</param>
        /// <param name="keyNormalizer">键标准化器</param>
        /// <param name="errors">错误描述</param>
        /// <param name="services">服务提供程序</param>
        /// <param name="logger">日志</param>
        public IdentityUserManager(IUserStore<User> store
            , IOptions<IdentityOptions> optionsAccessor
            , IPasswordHasher<User> passwordHasher
            , IEnumerable<IUserValidator<User>> userValidators
            , IEnumerable<IPasswordValidator<User>> passwordValidators
            , ILookupNormalizer keyNormalizer
            , IdentityErrorDescriber errors
            , IServiceProvider services
            , ILogger<UserManager<User>> logger)
            : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
        {
        }
    }
}
