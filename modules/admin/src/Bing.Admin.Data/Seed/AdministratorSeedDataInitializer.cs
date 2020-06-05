using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Biz.Enums;
using Bing.Datas.Seed;
using Bing.Datas.UnitOfWorks;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Data.Seed
{
    /// <summary>
    /// 管理员种子数据初始化
    /// </summary>
    public class AdministratorSeedDataInitializer : ISeedDataInitializer
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _rootProvider;

        /// <summary>
        /// 种子数据初始化顺序
        /// </summary>
        public virtual int Order => 0;

        /// <summary>
        /// 初始化一个<see cref="AdministratorSeedDataInitializer"/>类型的实例
        /// </summary>
        /// <param name="rootProvider">服务提供程序</param>
        public AdministratorSeedDataInitializer(IServiceProvider rootProvider) => _rootProvider = rootProvider;

        /// <summary>
        /// 初始化种子数据
        /// </summary>
        public async Task InitializeAsync()
        {
            var parameters = SeedData();
            await SyncToDatabaseAsync(parameters);
        }

        /// <summary>
        /// 重写以提供要初始化的种子数据
        /// </summary>
        protected UserParameter[] SeedData()
        {
            var list = new List<UserParameter>();
            list.Add(new UserParameter
            {
                Nickname = "超级管理员",
                UserName = "admin",
                Password = "123456",
                Enabled = true,
                Gender = Gender.Female,
                IsSystem = true
            });
            return list.ToArray();
        }

        /// <summary>
        /// 将种子数据初始化到数据库
        /// </summary>
        /// <param name="parameters">参数集合</param>
        protected async Task SyncToDatabaseAsync(UserParameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return;
            using var scope = _rootProvider.CreateScope();
            var scopeProvider = scope.ServiceProvider;
            using var unitOfWork = scopeProvider.GetService<IUnitOfWork>();
            var repository = scopeProvider.GetService<IUserRepository>();
            var manager = scopeProvider.GetService<IAdministratorManager>();
            var roleManager = scopeProvider.GetService<IRoleManager>();
            foreach (var parameter in parameters)
            {
                if (await repository.ExistsAsync(x => x.UserName == parameter.UserName))
                    continue;
                var user = await manager.CreateAsync(parameter);
                await roleManager.AddUserToRoleAsync(user.Id, RoleCode.SuperAdmin);
            }
            await unitOfWork.CommitAsync();
        }
    }
}
