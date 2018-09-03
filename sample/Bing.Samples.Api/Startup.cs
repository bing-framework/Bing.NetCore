using System;
using System.IO;
using System.Reflection;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.Exceptionless;
using Bing.Logs.NLog;
using Bing.Logs.Serilog;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
                options.Filters.Add<ExceptionHandlerAttribute>();
            }).AddControllersAsServices();
            //services.AddNLog();
            //services.AddExceptionless(options =>
            //{
            //    options.ApiKey = "YDTOG4uvUuEd5BY7uQozsUjaZcPyGz99OE6jNLmp";
            //    options.ServerUrl = "";
            //});
            services.AddSerilog();
            services.AddSwaggerGen(config =>
            {
                config.SwaggerDoc("v1", new Info() {Title = "Bing.Samples.Api", Version = "v1"});
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                var xmlPath = Path.Combine(basePath, "Bing.Samples.Api.xml");
                config.IncludeXmlComments(xmlPath);

                config.OperationFilter<AddRequestHeaderOperationFilter>();
                config.OperationFilter<AddResponseHeadersOperationFilter>();
                config.OperationFilter<AddFileParameterOperationFilter>();

                // 授权组合
                config.OperationFilter<AddSecurityRequirementsOperationFilter>();
                config.OperationFilter<AddAppendAuthorizeToSummaryOperationFilter>();
                config.AddSecurityDefinition("oauth2", new ApiKeyScheme()
                {
                    Description = "Token令牌",
                    In = "header",
                    Name = "Authorization",
                    Type = "apiKey",
                });
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", authBuilder =>
                {
                    authBuilder.AddAuthenticationSchemes("Bearer");
                    authBuilder.RequireRole("Admin");
                });
                options.AddPolicy("Customer", authBuilder =>
                {
                    authBuilder.AddAuthenticationSchemes("Bearer");
                    authBuilder.RequireRole("Customer");
                });
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
            app.UseStaticHttpContext();
            app.UseRequestLog();
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
