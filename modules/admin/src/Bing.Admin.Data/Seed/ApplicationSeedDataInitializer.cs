using System;
using System.Linq.Expressions;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Datas.Seed;
using Bing.Datas.UnitOfWorks;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Data.Seed
{
    /// <summary>
    /// 应用程序种子数据初始化
    /// </summary>
    public class ApplicationSeedDataInitializer : SeedDataInitializerBase<Application, Guid>
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _rootProvider;

        /// <summary>
        /// 初始化一个<see cref="SeedDataInitializerBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="rootProvider">服务提供程序</param>
        public ApplicationSeedDataInitializer(IServiceProvider rootProvider) : base(rootProvider)
        {
            _rootProvider = rootProvider;
        }

        /// <summary>
        /// 重写以提供要初始化的种子数据
        /// </summary>
        protected override Application[] SeedData() =>
            new[]
            {
                new Application(Guid.NewGuid())
                {
                    Code = ApplicationCode.Admin,
                    Name = "后台管理系统",
                    Enabled = true,
                    Remark = "Admin后台管理系统",
                    Creator = "系统初始化",
                    CreatorId = Guid.Empty,
                    LastModifier = "系统初始化",
                    LastModifierId = Guid.Empty,
                    ApplicationType = ApplicationType.General,
                },
            };

        /// <summary>
        /// 重写以提供判断某个实体是否存在的表达式
        /// </summary>
        /// <param name="entity">要判断的实体</param>
        protected override Expression<Func<Application, bool>> ExistingExpression(Application entity) => x => x.Code == entity.Code;

        /// <summary>
        /// 将种子数据初始化到数据库
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected override void SyncToDatabase(Application[] entities)
        {
            if (entities == null || entities.Length == 0)
                return;
            using var scope = _rootProvider.CreateScope();
            var scopeProvider = scope.ServiceProvider;
            using var unitOfWork = scopeProvider.GetService<IUnitOfWork>();
            var store = scopeProvider.GetService<IApplicationPoStore>();
            var repository = scopeProvider.GetService<IApplicationRepository>();
            foreach (var entity in entities)
            {
                if (store.Exists(x => x.Code == entity.Code))
                    continue;
                repository.Add(entity);
            }
            unitOfWork.Commit();
        }
    }
}
