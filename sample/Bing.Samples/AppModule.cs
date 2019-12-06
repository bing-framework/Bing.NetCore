using System.Collections.Generic;
using System.Text;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Core;
using Bing.Core.Modularity;
using Bing.Datas.Dapper;
using Bing.Datas.EntityFramework.MySql;
using Bing.Datas.Enums;
using Bing.Extensions.Swashbuckle.Configs;
using Bing.Extensions.Swashbuckle.Core;
using Bing.Extensions.Swashbuckle.Extensions;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.NLog;
using Bing.Samples.Data;
using Bing.Samples.EventHandlers.Abstractions;
using Bing.Samples.EventHandlers.Implements;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

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
            // 注册Swagger
            services.AddSwaggerCustom(SwaggerOptions);

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


            services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();

            // 注册工作单元
            services.AddMySqlUnitOfWork<ISampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.MySql.SampleUnitOfWork>(
                services.GetConfiguration().GetConnectionString("DefaultConnection"));

            // 注册SqlQuery
            services.AddSqlQuery<Bing.Samples.Data.UnitOfWorks.MySql.SampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.MySql.SampleUnitOfWork>(options =>
            {
                options.DatabaseType = DatabaseType.MySql;
                options.IsClearAfterExecution = true;
            });

            // 注册日志
            services.AddNLog();

            // 注册AutoMapper
            services.AddAutoMapper();
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
            app.UseSwaggerCustom(SwaggerOptions);
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            Enabled = true;
        }

        /// <summary>
        /// Swagger选项配置
        /// </summary>
        private CustomSwaggerOptions SwaggerOptions = new CustomSwaggerOptions()
        {
            ProjectName = "Bing.Samples 在线文档调试",
            UseCustomIndex = true,
            RoutePrefix = "swagger",
            ApiVersions =
                new List<Extensions.Swashbuckle.Configs.ApiVersion>()
                {
                    new Extensions.Swashbuckle.Configs.ApiVersion() {Version = "v1"}
                },
            SwaggerAuthorizations = new List<CustomSwaggerAuthorization>() { },
            AddSwaggerGenAction = config =>
            {
                //config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Samples.xml"), true);

                config.OperationFilter<RequestHeaderOperationFilter>();
                config.OperationFilter<ResponseHeadersOperationFilter>();
                config.OperationFilter<FileParameterOperationFilter>();

                // 授权组合
                config.OperationFilter<SecurityRequirementsOperationFilter>();
                config.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();
                config.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>()
                {
                    {"oauth2", new string[] { }}
                });

                config.AddSecurityDefinition("oauth2", new ApiKeyScheme()
                {
                    Description = "Token令牌",
                    In = "header",
                    Name = "Authorization",
                    Type = "apiKey",
                });
                // 设置所有参数为驼峰式命名
                config.DescribeAllParametersInCamelCase();
            },
            UseSwaggerAction = config =>
            {
            },
            UseSwaggerUIAction = config =>
            {
                config.InjectJavascript("/swagger/resources/jquery");
                config.InjectStylesheet("/swagger/resources/swagger-common");
                config.UseDefaultSwaggerUI();
            }
        };
    }
}
