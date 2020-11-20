using System.ComponentModel;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// AutoMapper 模块
    /// </summary>
    [Description("AutoMapper 模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class MapperModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddAutoMapper();
            return services;
        }
    }
}
