using System.Text;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Core;
using Bing.Core.Modularity;
using Bing.Datas.Dapper;
using Bing.Datas.EntityFramework.SqlServer;
using Bing.Datas.Enums;
using Bing.Events.Cap;
using Bing.Logs.NLog;
using Bing.Samples.Data;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Savorboard.CAP.InMemoryMessageQueue;

namespace Bing.Samples
{
    /// <summary>
    /// 应用程序模块
    /// </summary>
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
            services.AddSqlServerUnitOfWork<ISampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>(
                services.GetConfiguration().GetConnectionString("DefaultConnection"));

            // 注册SqlQuery
            services.AddSqlQuery<Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>(options =>
            {
                options.DatabaseType = DatabaseType.SqlServer;
                options.IsClearAfterExecution = true;
            });
            // 注册SqlExecutor
            services.AddSqlExecutor();

            // 注册日志
            services.AddNLog();

            // 注册AutoMapper
            services.AddAutoMapper();

            services.AddCapEventBus(o =>
            {
                o.UseEntityFramework<Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>();
                o.UseDashboard();
                // 设置处理成功的数据在数据库中保存的时间（秒），为保证系统性能，数据会定期清理
                o.SucceedMessageExpiredAfter = 24 * 3600;
                // 设置失败重试次数
                o.FailedRetryCount = 5;
                o.Version = "bing_test";
                // 启用内存队列
                o.UseInMemoryMessageQueue();
                // 启用RabbitMQ
                //o.UseRabbitMQ(x =>
                //{
                //    x.HostName = "";
                //    x.UserName = "admin";
                //    x.Password = "";
                //});
            });
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
