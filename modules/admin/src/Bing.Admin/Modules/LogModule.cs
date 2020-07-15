using System.ComponentModel;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Logs.Exceptionless;
using Bing.Logs.NLog;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 日志模块
    /// </summary>
    [Description("日志模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class LogModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 0;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            //services.AddNLog();
            services.AddExceptionless(o =>
            {
                o.ApiKey = "FKLLqe2hMowxb1udH8w76DaGjUxFw04BZv3P0AOO";
                o.ServerUrl = "http://106.12.130.45:50000";
            });
            return services;
        }
    }
}
