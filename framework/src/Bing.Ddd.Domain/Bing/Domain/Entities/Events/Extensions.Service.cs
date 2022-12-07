using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 领域事件扩展
/// </summary>
public static class Extensions
{
    /// <summary>
    /// 领域事件处理器基础类型
    /// </summary>
    private static readonly Type EventHandlerBaseType = typeof(IDomainEventHandler<>);

    /// <summary>
    /// 注册领域事件调度器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="types">类型列表</param>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services, Type[] types)
    {
        var assemblies = types.Select(x => x.Assembly).ToArray();
        services.AddDomainEventDispatcher(assemblies);
        return services;
    }

    /// <summary>
    /// 注册领域事件调度器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services)
    {
        var allAssemblyFinder = services.GetOrAddAllAssemblyFinder();
        return services.AddDomainEventDispatcher(allAssemblyFinder.FindAll());
    }

    /// <summary>
    /// 注册领域事件调度器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">程序集列表</param>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services, Assembly[] assemblies)
    {
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.TryAddScoped<IDomainHandlerFactory, DependencyInjectionHandlerFactory>();
        services.RegisterEventHandler(assemblies);
        return services;
    }

    /// <summary>
    /// 注册领域事件处理器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">程序集列表</param>
    private static void RegisterEventHandler(this IServiceCollection services, params Assembly[] assemblies)
    {
        var store = new DomainEventHandlerTypeStore();
        var types = assemblies.SelectMany(x => x.GetTypes());

        foreach (var type in types)
        {
            var interfaces = type.GetInterfaces();
            var handlerTypes = interfaces.Where(@interface => @interface.IsGenericType)
                .Where(@interface => EventHandlerBaseType == @interface.GetGenericTypeDefinition())
                .ToList();
            if (handlerTypes.Count == 0)
                continue;
            // 约束：一个领域事件处理器只能处理一个事件
            if (handlerTypes.Count > 1)
                throw new BingFrameworkException($"{type.FullName} 只能有一个领域事件处理器");
            var handlerType = handlerTypes[0];
            var eventType = handlerType.GenericTypeArguments[0];
            services.TryAddScoped(type);
            store.Add(eventType, type);
        }
        services.TryAddSingleton<IDomainEventHandlerTypeStore>(store);
    }
}