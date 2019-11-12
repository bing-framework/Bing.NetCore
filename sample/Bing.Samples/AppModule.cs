using System.Collections.Generic;
using System.Text;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Datas.Dapper;
using Bing.Datas.EntityFramework.SqlServer;
using Bing.Datas.Enums;
using Bing.Extensions.Swashbuckle.Configs;
using Bing.Extensions.Swashbuckle.Core;
using Bing.Extensions.Swashbuckle.Extensions;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.NLog;
using Bing.Modularity;
using Bing.Samples.Data;
using Bing.Samples.EventHandlers;
using Bing.Samples.Modules;
using Bing.Samples.Service;
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
    [DependsOn(typeof(BingAspNetCoreModule), typeof(SamplesServiceModule), typeof(SamplesEventHandlerModule),typeof(MiniProfilerModule))]
    public class AppModule : BingModule
    {
        /// <summary>
        /// 配置服务集合
        /// </summary>
        /// <param name="context">配置服务上下文</param>
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            // 注册Mvc
            context.Services
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
            // 注册Swagger
            context.Services.AddSwaggerCustom(SwaggerOptions);

            // 注册工作单元
            context.Services.AddSqlServerUnitOfWork<ISampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>(
                configuration.GetConnectionString("DefaultConnection"));

            // 注册SqlQuery
            context.Services.AddSqlQuery<Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork, Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>(options =>
            {
                options.DatabaseType = DatabaseType.SqlServer;
                options.IsClearAfterExecution = true;
            });

            // 注册日志
            context.Services.AddNLog();

            // 注册AutoMapper
            context.Services.AddAutoMapper();
        }

        /// <summary>
        /// 应用程序初始化
        /// </summary>
        /// <param name="context">应用程序初始化上下文</param>
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            app.UseErrorLog();
            app.UseStaticHttpContext();
            app.UseSwaggerCustom(SwaggerOptions);
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
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
