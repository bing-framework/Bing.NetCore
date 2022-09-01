using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Swashbuckle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// Swagger模块
    /// </summary>
    [Description("Swagger模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class SwaggerModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 2;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSwaggerEx(o =>
            {
                o.EnableCustomIndex = true;
                o.EnableCached = true;
                AddSwaggerEx(o);
                o.AddSwaggerGenAction = config =>
                {
                    AddSwaggerGen(config);
                    //config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Admin.Service.xml"));
                    //config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Admin.Infrastructure.xml"));

                    #region 启用Swagger验证功能

                    // 启用Swagger验证功能，与AddSecurityDefinition方法指定的方案名称一致
                    config.AddSecurityRequirement(new OpenApiSecurityRequirement()
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "oauth2"}
                            },
                            new[] {"readAccess", "writeAccess"}
                        }
                    });

                    config.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme()
                    {
                        Type = SecuritySchemeType.ApiKey,
                        Description = "Token令牌，JWT授权(数据将在请求头中进行传输)在下方输入Bearer {token} 即可，注意两者之间有空格",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                    });

                    #endregion
                    // 显示枚举描述
                    config.ShowEnumDescription();
                    // 控制器排序
                    config.OrderByController();

                    config.DescribeAllParametersInCamelCase();
                    config.MapType<IFormFile>(() => new OpenApiSchema { Type = "file" });
                };
            });
            services.AddSwaggerGenNewtonsoftSupport();
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            app.UseSwaggerEx(o =>
            {
                o.UseSwaggerAction = UseSwagger;
                o.UseSwaggerUIAction = config =>
                {
                    UseSwaggerUI(config);
                    config.UseDefaultSwaggerUI();
                    config.UseTokenStorage("oauth2");
                };
            });
            Enabled = true;
        }

        /// <summary>
        /// 添加Swagger扩展配置
        /// </summary>
        /// <param name="options">配置</param>
        protected virtual void AddSwaggerEx(SwaggerExOptions options)
        {
            options.ApiVersions.Add(new ApiVersion { Description = "默认", Version = "v1" });
            options.ProjectName = "Bing.Admin 在线文档";
        }

        /// <summary>
        /// 添加Swagger生成配置
        /// </summary>
        /// <param name="options">配置</param>
        protected virtual void AddSwaggerGen(SwaggerGenOptions options)
        {
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Admin.xml"), true);
        }
        /// <summary>
        /// 启用Swagger配置
        /// </summary>
        /// <param name="options">配置</param>
        protected virtual void UseSwagger(SwaggerOptions options)
        {
            options.PreSerializeFilters.Add((swaggerDoc, httpRequest) =>
            {
                if (string.IsNullOrWhiteSpace(httpRequest.Headers["X-Forwarded-Prefix"]))
                    return;
                var serverUrl =
                    $"{httpRequest.Scheme}://{httpRequest.Host.Value}/{httpRequest.Headers["X-Forwarded-Prefix"]}";
                swaggerDoc.Servers = new List<OpenApiServer>
                {
                    new OpenApiServer {Url = serverUrl}
                };
            });
        }

        /// <summary>
        /// 启用SwaggerUI配置
        /// </summary>
        /// <param name="options">配置</param>
        // ReSharper disable once InconsistentNaming
        protected virtual void UseSwaggerUI(SwaggerUIOptions options)
        {
            //options.SwaggerEndpoint("v1/swagger.json", "v1");
        }
    }
}
