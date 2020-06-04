using System;
using AspectCore.Extensions.DependencyInjection;
using Bing.Admin.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Admin
{
    /// <summary>
    /// 启动配置
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// 初始化一个<see cref="Startup"/>类型的是
        /// </summary>
        /// <param name="configuration">配置</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// 配置
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// 配置服务
        /// </summary>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            //services.AddBing<AspNetCoreBingModuleManager>();
            services.AddBing()
                .AddModule<LogModule>()
                .AddModule<MapperModule>()
                .AddModule<AppModule>()
                .AddModule<PgSqlAdminUnitOfWorkMigrationModule>()
                .AddModule<SwaggerModule>();
            return services.BuildServiceContextProvider();
        }

        /// <summary>
        /// 配置请求管道
        /// </summary>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSysLogProvider();
            app.UseBing();
        }
    }
}
