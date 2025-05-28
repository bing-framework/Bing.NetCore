﻿using System.ComponentModel;
using System.Text;
using AspectCore.Configuration;
using Bing.AspNetCore;
using Bing.AspNetCore.Mvc.ExceptionHandling;
using Bing.AspNetCore.Mvc.Filters;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Helpers;
using Bing.Locks;
using Bing.Security.Claims;
using Bing.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 应用程序模块
    /// </summary>
    [Description("应用程序模块")]
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
            BingClaimTypes.UserId = IdentityModel.JwtClaimTypes.Subject;
            BingClaimTypes.UserName = IdentityModel.JwtClaimTypes.Name;
            // 注册Mvc
            services.AddControllers(o =>
                {
                    o.Filters.Add<ValidationModelAttribute>();
                    o.Filters.Add<ResultHandlerAttribute>();
                    o.Filters.Add<BingExceptionFilter>();
                    o.Conventions.Add(new AuthorizeControllerModelConvention());
                })
                .AddControllersAsServices()// 解决属性注入无法在控制器中注入的问题
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                });
            // 配置模型校验
            services.Configure<ApiBehaviorOptions>(o =>
            {
                o.SuppressModelStateInvalidFilter = true;
            });
            services.EnableAop(o =>
            {
                o.ThrowAspectException = false;
                o.NonAspectPredicates.AddNamespace("Bing.Swashbuckle");
                o.NonAspectPredicates.AddNamespace("DotNetCore.CAP");
            });
            services.RegisterAspectScoped();
            services.AddDomainEventDispatcher();
            services.AddLocalLock();
            services.AddRedisDistributedLock();
            // 添加跟踪ID
            services.Configure<CorrelationIdOptions>(x =>
            {
                x.HttpHeaderName = "X-Correlation-Id";
                x.SetResponseHeader = true;
            });
            //services.AddAudit();
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            app.UseCorrelationId();
            app.UseBingExceptionHandling();
            // 初始化Http上下文访问器
            Web.HttpContextAccessor = app.ApplicationServices.GetService<IHttpContextAccessor>();
            app.UseAuthentication();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            Enabled = true;
        }
    }

    /// <summary>
    /// 授权控制器模型转换器
    /// </summary>
    internal class AuthorizeControllerModelConvention : IControllerModelConvention
    {
        /// <summary>
        /// 实现Apply
        /// </summary>
        public void Apply(ControllerModel controller) => controller.Filters.Add(new AuthorizeFilter("jwt"));
    }
}
