using System;
using System.ComponentModel;
using Bing.Admin.Data.Seed;
using Bing.Admin.Data.UnitOfWorks.PgSql;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Datas.EntityFramework;
using Bing.Datas.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// PgSql-AdminUnitOfWork迁移模块
    /// </summary>
    [DependsOnModule(typeof(AspNetCoreModule))]
    [Description("PgSql-AdminUnitOfWork迁移模块")]
    public class PgSqlAdminUnitOfWorkMigrationModule : MigrationModuleBase<AdminUnitOfWork>
    {
        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 2;

        /// <summary>
        /// 重写实现获取数据上下文实例
        /// </summary>
        /// <param name="scopedProvider">服务提供者</param>
        protected override AdminUnitOfWork CreateDbContext(IServiceProvider scopedProvider) => scopedProvider.GetService<AdminUnitOfWork>();

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            // 种子数据初始化
            services.AddSingleton<ISeedDataInitializer, ApplicationSeedDataInitializer>();
            services.AddSingleton<ISeedDataInitializer, RoleSeedDataInitializer>();
            services.AddSingleton<ISeedDataInitializer, AdministratorSeedDataInitializer>();
            return services;
        }
    }
}
