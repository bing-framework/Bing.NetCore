using System;
using System.Collections.Generic;
using System.Text;
using Bing.Events.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Events.Default
{
    /// <summary>
    /// 事件总线扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册默认事件总线服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns></returns>
        public static IServiceCollection AddDefaultEventBus(this IServiceCollection services)
        {
            return services.AddSingleton<IEventHandlerManager, EventHandlerManager>()
                .AddSingleton<IEventBus, EventBus>();
        }
    }
}
