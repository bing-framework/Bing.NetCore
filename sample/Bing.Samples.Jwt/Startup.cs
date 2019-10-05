using System;
using System.Collections.Generic;
using System.Text;
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
using Bing.Permissions.Authorization.Policies;
using Bing.Permissions.Extensions;
using Bing.Permissions.Identity.JwtBearer;
using EasyCaching.Core;
using EasyCaching.CSRedis;
using EasyCaching.Serialization.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
            services.AddMvc(options =>
            {
                // 全局添加授权
                options.Conventions.Add(new AuthorizeControllerModelConvention());
            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_2).AddControllersAsServices();
            
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

            var jwtOptions = Configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("jwt", policy => policy.Requirements.Add(new JsonWebTokenAuthorizationRequirement()));
            }).AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o =>
            {
                o.TokenValidationParameters = GetValidationParameters(jwtOptions);
                o.SaveToken = true;
            });
            services.AddJwt(Configuration);

            services.AddSwaggerCustom(CurrentSwaggerOptions);
            services.AddBing<AspNetCoreBingModuleManager>();
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 获取验证参数
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        private static TokenValidationParameters GetValidationParameters(JwtOptions options)
        {
            return new TokenValidationParameters()
            {
                ValidateIssuerSigningKey = true, // 是否验证发行者签名密钥
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
                ValidateIssuer = true, // 是否验证发行者
                ValidIssuer = options.Issuer,
                ValidateAudience = true, // 是否验证接收者
                ValidAudience = options.Audience,
                ValidateLifetime = true, // 是否验证超时
                LifetimeValidator = (before, expires, token, param) => expires > DateTime.UtcNow,
                ClockSkew = TimeSpan.FromSeconds(30), // 缓冲过期时间，总的有效时间等于该时间加上Jwt的过期时间，如果不配置，则默认是5分钟
                RequireExpirationTime = true
            };
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
            app.UseAuthentication();
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

    /// <summary>
    /// 授权控制器模型转换器
    /// </summary>
    public class AuthorizeControllerModelConvention : IControllerModelConvention
    {
        /// <summary>
        /// 实现Apply
        /// </summary>
        public void Apply(ControllerModel controller)
        {
            controller.Filters.Add(new AuthorizeFilter("jwt"));
        }
    }
}
