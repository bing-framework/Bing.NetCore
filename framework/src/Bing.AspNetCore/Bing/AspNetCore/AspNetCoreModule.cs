using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore
{
    /// <summary>
    /// AspNetCore模块
    /// </summary>
    [Description("AspNetCore模块")]
    public class AspNetCoreModule : Bing.Core.Modularity.BingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override Bing.Core.Modularity.ModuleLevel Level => Bing.Core.Modularity.ModuleLevel.Core;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// </summary>
        public override int Order => 2;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            // 注入Http上下文用户会话
            services.TryAddScoped<Bing.Application.ISession, HttpContextSession>();

            // 注入当前用户，替换Thread.CurrentPrincipal的作用
            services.AddTransient<System.Security.Principal.IPrincipal>(provider =>
            {
                var accessor = provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                return accessor?.HttpContext?.User;
            });
            // 注入用户会话
            services.AddSingleton<Bing.Sessions.ISession, Bing.Sessions.Session>();
            // 注册编码
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return services;
        }

        /// <summary>
        /// 应用模块服务
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        public override void UseModule(IServiceProvider provider) => Enabled = true;
    }
}
