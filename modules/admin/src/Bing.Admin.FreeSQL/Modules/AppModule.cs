using System.ComponentModel;
using System.Text;
using AspectCore.Configuration;
using Bing.AspNetCore;
using Bing.AspNetCore.Extensions;
using Bing.AspNetCore.Mvc.Filters;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Helpers;
using Bing.Security.Claims;
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
            services
                .AddMvc(options =>
                {
                    //options.Filters.Add<ResultHandlerAttribute>();
                    options.Filters.Add<ExceptionHandlerAttribute>();
                    //options.Filters.Add<AuditOperationAttribute>();
                    // 全局添加授权
                    options.Conventions.Add(new AuthorizeControllerModelConvention());
                })
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddControllersAsServices();
            services.EnableAop(o =>
            {
                o.ThrowAspectException = false;
                o.NonAspectPredicates.AddNamespace("Bing.Swashbuckle");
                o.NonAspectPredicates.AddNamespace("DotNetCore.CAP");
            });
            services.AddDomainEventDispatcher();
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
            app.UseBingExceptionHandling();
            // 初始化Http上下文访问器
            Web.HttpContextAccessor = app.ApplicationServices.GetService<IHttpContextAccessor>();
            app.UseAuthentication();
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
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
