using Bing.Samples.EventHandlers.Abstractions;
using Bing.Samples.EventHandlers.Implements;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.EventHandlers
{
    /// <summary>
    /// 消息事件 启动配置
    /// </summary>
    public static class MessageEventStartup
    {
        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void Config(IServiceCollection services)
        {
            services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();
        }
    }
}
