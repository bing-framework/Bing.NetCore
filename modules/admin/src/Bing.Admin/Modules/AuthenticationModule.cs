using System.ComponentModel;
using Bing.Admin.Service.Extensions;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 身份认证模块
    /// </summary>
    [Description("身份认证模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class AuthenticationModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 1;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            // 添加权限服务
            services.AddPermission(o =>
            {
                o.Store.StoreOriginalPassword = true;
                o.Password.MinLength = 6;
            });
            return services;
        }
    }
}
