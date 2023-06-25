using Bing.Events.Default;
using Bing.Events.Handlers;
using Bing.Events.Messages;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Cap;

/// <summary>
/// 事件总线扩展
/// </summary>
public static class Extensions
{
    /// <summary>
    /// 注册CAP事件总线服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="action">配置操作</param>
    /// <param name="builderSetup">CAP构建器配置操作</param>
    public static IServiceCollection AddCapEventBus(this IServiceCollection services, Action<CapOptions> action, Action<CapBuilder> builderSetup = null)
    {
        services.TryAddSingleton<IEventHandlerManager, EventHandlerManager>();
        services.TryAddSingleton<ISimpleEventBus, Default.EventBus>();
        services.TryAddScoped<IMessageEventBus, MessageEventBus>();
        services.TryAddScoped<IEventBus, EventBus>();
        var builder = services.AddCap(action);
        builderSetup?.Invoke(builder);
        return services;
    }
}
