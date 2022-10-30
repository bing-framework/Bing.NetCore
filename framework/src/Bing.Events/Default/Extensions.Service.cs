using Bing.Events.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Default;

/// <summary>
/// 事件总线扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册默认事件总线服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDefaultEventBus(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventHandlerManager, EventHandlerManager>();
        services.TryAddSingleton<ISimpleEventBus, EventBus>();
        services.TryAddSingleton<IEventBus, EventBus>();
        return services;
    }
}