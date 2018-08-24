using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bing.Logs.Exceptionless;
using Bing.Samples.Api.SwaggerExtensions;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;

namespace Bing.Samples.Api
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(options =>
            {
                options.Filters.Add<ValidationAttribute>();
                options.Filters.Add<ResultHandlerAttribute>();
            }).AddControllersAsServices();
            services.AddExceptionless(options =>
            {
                options.ApiKey = "YDTOG4uvUuEd5BY7uQozsUjaZcPyGz99OE6jNLmp";
                options.ServerUrl = "";
            });
            services.AddSwaggerGen(config =>
            {
                config.SwaggerDoc("v1", new Info() {Title = "Bing.Samples.Api", Version = "v1"});
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                var xmlPath = Path.Combine(basePath, "Bing.Samples.Api.xml");
                config.IncludeXmlComments(xmlPath);
                //config.OperationFilter<AddAuthTokenHeaderParameter>();
            });
            //services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
            return services.AddBing();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            CommonConfig(app);

            app.UseSwagger(config => { });
            app.UseSwaggerUI(config =>
            {
                config.IndexStream = () =>
                    GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Bing.Samples.Api.Swagger.index.html");
                config.ShowExtensions();
                config.SwaggerEndpoint("/swagger/v1/swagger.json", "Bing.Samples.Api v1");                
            });
        }

        /// <summary>
        /// 公共配置
        /// </summary>
        /// <param name="app"></param>
        private void CommonConfig(IApplicationBuilder app)
        {
            app.UseErrorLog();
            ConfigRoute(app);
        }

        /// <summary>
        /// 路由配置，支持区域
        /// </summary>
        /// <param name="app"></param>
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
