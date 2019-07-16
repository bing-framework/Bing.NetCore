using System.ComponentModel;
using System.Security.Principal;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore
{
    /// <summary>
    /// AspNetCore模块
    /// </summary>
    [Description("AspNetCore模块")]
    public class AspNetCoreModule : BingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Core;

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
            // 注入当前用户，替换Thread.CurrentPrincipal的作用
            services.AddTransient<IPrincipal>(provider =>
            {
                var accessor = provider.GetService<IHttpContextAccessor>();
                return accessor?.HttpContext?.User;
            });
            return services;
        }
    }
}
