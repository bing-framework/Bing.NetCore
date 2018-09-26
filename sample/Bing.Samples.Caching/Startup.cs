using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bing.Caching.InMemory;
using Bing.Caching.Internal;
using Bing.Caching.Redis;
using Bing.Logs.NLog;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;

namespace Bing.Samples.Caching
{
    /// <summary>
    /// 启动配置
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// 配置服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns></returns>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            // 添加MVC服务
            services
                .AddMvc(options =>
                {
                    options.Filters.Add<ResultHandlerAttribute>();
                    options.Filters.Add<ExceptionHandlerAttribute>();
                })
                .AddControllersAsServices();

            // 添加NLog日志操作
            services.AddNLog();

            // 添加Swagger
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info() {Title = "Bing.Sample.Caching", Version = "v1"});
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Samples.Caching.xml"));
            });

            // 添加内存缓存操作
            //services.AddDefaultInMemoryCache();

            // 添加Redis缓存操作
            services.AddDefaultRedisCache(x =>
            {
                x.DbConfig.EndPoints.Add(new ServerEndPoint("192.168.205.129",6379));
            });

            // 添加Bing基础设施服务
            return services.AddBing();
        }

        /// <summary>
        /// 配置请求管道
        /// </summary>
        /// <param name="app">应用构建器</param>
        /// <param name="env">主机环境</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                DevelopmentConfig(app);
                return;
            }
            ProductionConfig(app);
        }

        /// <summary>
        /// 开发环境配置
        /// </summary>
        private void DevelopmentConfig(IApplicationBuilder app)
        {
            CommonConfig(app);
        }

        /// <summary>
        /// 生产环境配置
        /// </summary>
        private void ProductionConfig(IApplicationBuilder app)
        {
            CommonConfig(app);
        }

        /// <summary>
        /// 公共配置
        /// </summary>
        private void CommonConfig(IApplicationBuilder app)
        {
            ConfigSwagger(app);
            ConfigRoute(app);
        }

        /// <summary>
        /// Swagger配置
        /// </summary>
        private void ConfigSwagger(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(config =>
            {
                config.ShowExtensions();
                config.SwaggerEndpoint("/swagger/v1/swagger.json", "Bing.Samples.Caching v1");
            });
        }

        /// <summary>
        /// 路由配置，支持区域
        /// </summary>
        private void ConfigRoute(IApplicationBuilder app)
        {
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
