using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Permissions.Identity.Options;
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
        public UserManager(IdentityUserManager userManager
            , IOptions<PermissionOptions> options
            , IUserRepository userRepository
            , IUserInfoRepository userInfoRepository)
            : base(userManager, options, userRepository)
        {
            UserRepository = userRepository;
            UserInfoRepository = userInfoRepository;
        }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; }

        /// <summary>
        /// 用户信息仓储
        /// </summary>
        protected IUserInfoRepository UserInfoRepository { get; }

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">标识列表</param>
        public async Task EnableAsync(List<Guid> ids)
        {
            var entities = await UserRepository.FindByIdsAsync(ids);
            foreach (var entity in entities) 
                entity.Enabled = true;
            await UserRepository.UpdateAsync(entities);
        }

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">标识列表</param>
        public async Task DisableAsync(List<Guid> ids)
        {
            var entities = await UserRepository.FindByIdsAsync(ids);
            foreach (var entity in entities) 
                entity.Enabled = false;
            await UserRepository.UpdateAsync(entities);
        }
    }
}
