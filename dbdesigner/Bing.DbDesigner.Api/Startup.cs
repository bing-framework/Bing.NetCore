using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bing.Datas.EntityFramework.SqlServer;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Data.UnitOfWorks.SqlServer;
using Bing.DbDesigner.Service.Configs;
using Bing.Events.Default;
using Bing.Logs.NLog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;

namespace Bing.DbDesigner.Api
{
    /// <summary>
    /// 启动配置
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// 初始化一个<see cref="Startup"/>类型的实例
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
            // 添加MVC服务
            services.AddMvc(options =>
            {

            }).AddControllersAsServices();

            // 添加NLog日志操作
            services.AddNLog();

            // 添加事件总线服务
            services.AddDefaultEventBus();

            // 添加工作单元
            services.AddSqlServerUnitOfWork<IDbDesignerUnitOfWork, DbDesignerUnitOfWork>(
                Configuration.GetConnectionString("DefaultConnection"));

            // 添加Swagger
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info() {Title = "Bing数据库设计系统", Version = "v1"});
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.DbDesigner.Api.xml"));
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.DbDesigner.Service.xml"));
            });

            // 添加Bing基础设施服务
            return services.AddBing(new IocConfig());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            app.UseSwagger(config => { });
            app.UseSwaggerUI(config =>
            {
                config.SwaggerEndpoint("/swagger/v1/swagger.json","Bing.DbDesigner.Api v1");
            });
        }
    }
}
