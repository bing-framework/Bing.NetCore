﻿using Bing.Admin.Modules;
using Bing.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBing()
                .AddModule<LogModule>()
                .AddModule<MapperModule>()
                .AddModule<AppModule>()
                .AddModule<AuthenticationModule>()
                .AddModule<FreeSqlModule>()
                //.AddModule<EntityFrameworkCoreModule>()
                .AddModule<CacheModule>()
                .AddModule<CapModule>()
                //.AddModule<MySqlAdminUnitOfWorkMigrationModule>()
                .AddModule<SwaggerModule>();
        }

        /// <summary>
        /// 配置请求管道
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Web.Environment = env;
            app.UseBing();
        }
    }
}
