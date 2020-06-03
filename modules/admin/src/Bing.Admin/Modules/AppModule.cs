using System.ComponentModel;
using System.Text;
using Bing.Admin.Data;
using Bing.Admin.Data.Seed;
using Bing.Admin.Service.Extensions;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Datas.Dapper;
using Bing.Datas.EntityFramework.PgSql;
using Bing.Datas.Enums;
using Bing.Datas.Seed;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 应用程序模块
    /// </summary>
    [Description("应用程序模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class AppModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            // 注册Mvc
            services
                .AddMvc(options =>
                {
                    options.Filters.Add<ResultHandlerAttribute>();
                    options.Filters.Add<ExceptionHandlerAttribute>();
                })
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddControllersAsServices();

            // 注册工作单元
            services.AddPgSqlUnitOfWork<IAdminUnitOfWork, Bing.Admin.Data.UnitOfWorks.PgSql.AdminUnitOfWork>(
                services.GetConfiguration().GetConnectionString("DefaultConnection"));

            // 注册SqlQuery
            services.AddSqlQuery<Bing.Admin.Data.UnitOfWorks.PgSql.AdminUnitOfWork, Bing.Admin.Data.UnitOfWorks.PgSql.AdminUnitOfWork>(options =>
            {
                options.DatabaseType = DatabaseType.PgSql;
                options.IsClearAfterExecution = true;
            });
            // 注册SqlExecutor
            services.AddSqlExecutor();

            // 添加权限服务
            services.AddPermission(o =>
            {
                o.Store.StoreOriginalPassword = true;
                o.Password.MinLength = 6;
            });

            // 种子数据初始化
            services.AddSingleton<ISeedDataInitializer, ApplicationSeedDataInitializer>();
            services.AddSingleton<ISeedDataInitializer, RoleSeedDataInitializer>();

            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            app.UseErrorLog();
            app.UseStaticHttpContext();
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            Enabled = true;
        }
    }
}
