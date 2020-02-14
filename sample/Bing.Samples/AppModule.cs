using System.Collections.Generic;
using System.Text;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Core;
using Bing.Core.Modularity;
using Bing.Datas.Dapper;
using Bing.Datas.EntityFramework.SqlServer;
using Bing.Datas.Enums;
using Bing.Events.Cap;
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
using Savorboard.CAP.InMemoryMessageQueue;
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
