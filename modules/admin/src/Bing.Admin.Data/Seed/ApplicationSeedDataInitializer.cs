using System;
using System.Linq.Expressions;
using Bing.Admin.Systems.Domain.Models;
using Bing.Datas.Seed;

namespace Bing.Admin.Data.Seed
{
    /// <summary>
    /// 应用程序种子数据初始化
    /// </summary>
    public class ApplicationSeedDataInitializer : SeedDataInitializerBase<Application, Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="SeedDataInitializerBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="rootProvider">服务提供程序</param>
        public ApplicationSeedDataInitializer(IServiceProvider rootProvider) : base(rootProvider)
        {
        }

        /// <summary>
        /// 重写以提供要初始化的种子数据
        /// </summary>
        protected override Application[] SeedData() =>
            new[]
            {
                new Application(Guid.NewGuid())
                {
                    Code = "Bing.Admin.Admin",
                    Name = "后台管理系统",
                    Enabled = true,
                    Remark = "Admin后台管理系统",
                    Creator = "系统初始化",
                    CreatorId = Guid.Empty,
                    LastModifier = "系统初始化",
                    LastModifierId = Guid.Empty
                },
            };

        /// <summary>
        /// 重写以提供判断某个实体是否存在的表达式
        /// </summary>
        /// <param name="entity">要判断的实体</param>
        protected override Expression<Func<Application, bool>> ExistingExpression(Application entity) => x => x.Code == entity.Code;
    }
}
