using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Samples.EventHandlers.Abstractions;
using Bing.Samples.EventHandlers.Implements;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.EventHandlers
{
    /// <summary>
    /// Sample 事件处理器模块
    /// </summary>
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class SamplesEventHandlerModule : AspNetCoreBingModule
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
            //services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            Enabled = true;
        }
    }
}
