using Bing.Modularity;
using Bing.Samples.Data;
using Bing.Samples.Domain;
using Bing.Samples.EventHandlers.Abstractions;
using Bing.Samples.EventHandlers.Implements;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.EventHandlers
{
    /// <summary>
    /// Sample 事件处理器模块
    /// </summary>
    [DependsOn(typeof(SamplesDataModule), typeof(SamplesDomainModule))]
    public class SamplesEventHandlerModule : BingModule
    {
        /// <summary>
        /// 预配置服务集合
        /// </summary>
        /// <param name="context">服务配置上下文</param>
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            LoadEventHandler(context.Services);
        }

        /// <summary>
        /// 加载事件处理器
        /// </summary>
        /// <param name="services">服务集合</param>
        private void LoadEventHandler(IServiceCollection services)
        {
            services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();
        }
    }
}
