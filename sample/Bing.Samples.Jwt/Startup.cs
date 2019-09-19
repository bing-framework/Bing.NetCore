using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bing.AspNetCore;
using Bing.Core;
using Bing.EasyCaching;
using Bing.Extensions.Swashbuckle.Configs;
using Bing.Extensions.Swashbuckle.Core;
using Bing.Extensions.Swashbuckle.Extensions;
using Bing.Extensions.Swashbuckle.Filters.Documents;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.NLog;
using Bing.Security.Extensions;
using EasyCaching.Core;
using EasyCaching.CSRedis;
using EasyCaching.Serialization.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Bing.Samples.Jwt
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// 配置服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2).AddControllersAsServices();
            services.AddJwt(Configuration);
            services.AddNLog();
            // 添加EasyCaching缓存
            services.AddCaching(options =>
            {
                options.UseCSRedis(config =>
                {
                    // 互斥锁的存活时间。默认值:5000
                    config.LockMs = 5000;
                    // 预防缓存在同一时间全部失效，可以为每个key的过期时间添加一个随机的秒数。默认值:120秒
                    config.MaxRdSecond = 120;
                    // 是否开启日志。默认值:false
                    config.EnableLogging = false;
                    // 没有获取到互斥锁时的休眠时间。默认值:300毫秒
                    config.SleepMs = 300;
                    config.DBConfig = new CSRedisDBOptions()
                    {
                        ConnectionStrings = new List<string>()
                        {
                            Configuration.GetConnectionString("RedisConnection")
                        }
                    };
                }).WithJson();
            });

            services.AddSwaggerCustom(CurrentSwaggerOptions);
            services.AddBing<AspNetCoreBingModuleManager>();
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 配置请求管道
        /// </summary>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseEasyCaching();
            app.UseSwaggerCustom(CurrentSwaggerOptions);
            ConfigRoute(app);
            app.UseBing();
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
            app.Run(context =>
            {
                context.Response.Redirect("/swagger");
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// 项目接口文档配置
        /// </summary>
        private CustomSwaggerOptions CurrentSwaggerOptions = new CustomSwaggerOptions()
        {
            ProjectName = "Bing.Samples.Jwt 在线文档调试",
            UseCustomIndex = true,
            RoutePrefix = "swagger",
            ApiVersions = new List<Extensions.Swashbuckle.Configs.ApiVersion>() { new Extensions.Swashbuckle.Configs.ApiVersion() { Description = "", Version = "v1" } },
            SwaggerAuthorizations = new List<CustomSwaggerAuthorization>()
            {
            },
            AddSwaggerGenAction = config =>
            {
                //config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Samples.Jwt.xml"), true);

                config.OperationFilter<RequestHeaderOperationFilter>();
                config.OperationFilter<ResponseHeadersOperationFilter>();
                config.OperationFilter<FileParameterOperationFilter>();

                // 授权组合
                config.OperationFilter<SecurityRequirementsOperationFilter>();
                config.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();

                #region 启用Swagger验证功能

                // 启用Swagger验证功能，与AddSecurityDefinition方法指定的方案名称一致
                config.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>()
                    {{"oauth2", new string[] { }}});

                config.AddSecurityDefinition("oauth2", new ApiKeyScheme()
                {
                    Description = "Token令牌，JWT授权(数据将在请求头中进行传输)在下方输入Bearer {token} 即可，注意两者之间有空格",
                    In = "header",
                    Name = "Authorization",
                    Type = "apiKey",
                });

                #endregion 启用Swagger验证功能

                // 设置所有参数为驼峰式命名
                config.DescribeAllParametersInCamelCase();
                // 显示枚举描述
                config.DocumentFilter<AddEnumDescriptionsDocumentFilter>();
                // 显示首字母小写Url
                config.ShowUrlMode();
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
