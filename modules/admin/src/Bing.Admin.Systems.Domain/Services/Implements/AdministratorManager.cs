using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Domain.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Parameters.Extensions;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Exceptions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 管理员 管理
    /// </summary>
    public class AdministratorManager : DomainServiceBase, IAdministratorManager
    {
        /// <summary>
        /// 初始化一个<see cref="AdministratorManager"/>类型的实例
        /// </summary>
        public AdministratorManager(IAdministratorRepository administratorRepository
            , IUserRepository userRepository
            , IUserInfoRepository userInfoRepository
            , IUserManager userManager)
        {
            AdministratorRepository = administratorRepository;
            UserRepository = userRepository;
            UserInfoRepository = userInfoRepository;
            UserManager = userManager;
        }

        /// <summary>
        /// 管理员仓储
        /// </summary>
        protected IAdministratorRepository AdministratorRepository { get; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; }

        /// <summary>
        /// 用户信息仓储
        /// </summary>
        protected IUserInfoRepository UserInfoRepository { get; }

        /// <summary>
        /// 用户管理
        /// </summary>
        protected IUserManager UserManager { get; }

        #region CreateAsync(创建)

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="parameter">参数</param>
        public async Task<User> CreateAsync(UserParameter parameter)
        {
            if (await UserRepository.ExistsAsync(x => x.UserName == parameter.UserName))
                throw new Warning("账户已存在");
            return await CreateUserAndAdministratorAsync(parameter);
        }

        /// <summary>
        /// 创建用户以及管理员
        /// </summary>
        /// <param name="parameter">参数</param>
        private async Task<User> CreateUserAndAdministratorAsync(UserParameter parameter)
        {
            var user = parameter.ToUser();
            await UserManager.CreateAsync(user, parameter.Password);
            await CreateAsync(parameter.ToAdministrator(user.Id));
            await CreateUserInfoAsync(parameter.ToUserInfo(user.Id));
            return user;
        }

        /// <summary>
        /// 创建管理员
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task CreateAsync(Administrator entity)
        {
            entity.Init();
            await AdministratorRepository.AddAsync(entity);
        }

        /// <summary>
        /// 创建用户信息
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task CreateUserInfoAsync(UserInfo entity)
        {
            entity.Init();
            await UserInfoRepository.AddAsync(entity);
        }

        #endregion

        #region UpdateAsync(更新)

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="parameter">参数</param>
        public async Task UpdateAsync(UserParameter parameter)
        {
            var entity = await AdministratorRepository.FindAsync(parameter.Id);
            if (entity == null)
                throw new Warning("用户不存在");
            entity.Update(parameter);
            await UpdateUserAsync(parameter);
            await UpdateUserInfoAsync(parameter);
        }

        /// <summary>
        /// 更新用户
        /// </summary>
        /// <param name="parameter">参数</param>
        private async Task UpdateUserAsync(UserParameter parameter)
        {
            var entity = await UserRepository.FindAsync(parameter.Id);
            if (entity == null)
                throw new Warning("用户不存在");
            entity.Nickname = parameter.Nickname;
            entity.Enabled = parameter.Enabled;
            await UserRepository.UpdateAsync(entity);
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="parameter">参数</param>
        private async Task UpdateUserInfoAsync(UserParameter parameter)
        {
            var entity = await UserInfoRepository.FindAsync(parameter.Id);
            if (entity == null)
                throw new Warning("用户不存在");
            entity.Name = parameter.Nickname;
            entity.Gender = parameter.Gender;
            await UserInfoRepository.UpdateAsync(entity);
        }

        #endregion

        #region EnableAsync(启用)

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        public async Task EnableAsync(List<Guid> ids)
        {
            var entities = await AdministratorRepository.FindByIdsAsync(ids);
            foreach (var entity in entities)
            {
                entity.Enabled = true;
            }
            await AdministratorRepository.UpdateAsync(entities);
            await UserManager.EnableAsync(ids);
        }

        #endregion

        #region DisableAsync(禁用)

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        public async Task DisableAsync(List<Guid> ids)
        {
            var entities = await AdministratorRepository.FindByIdsAsync(ids);
            foreach (var entity in entities)
            {
                entity.Enabled = false;
            }
            await AdministratorRepository.UpdateAsync(entities);
            await UserManager.DisableAsync(ids);
        }

        #endregion
    }
}
