using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 提供领域事件调度器和处理器的依赖注入注册扩展。
/// </summary>
public static class Extensions
{
    /// <summary>
    /// 用于识别领域事件处理器的泛型接口类型。
    /// </summary>
    private static readonly Type _eventHandlerBaseType = typeof(IDomainEventHandler<>);

    /// <summary>
    /// 从指定类型所属程序集注册领域事件调度器及处理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="types">用于定位待扫描程序集的类型集合。</param>
    /// <returns>传入的服务集合。</returns>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services, Type[] types)
    {
        var assemblies = types.Select(x => x.Assembly).ToArray();
        services.AddDomainEventDispatcher(assemblies);
        return services;
    }

    /// <summary>
    /// 从当前服务集合关联的全部程序集注册领域事件调度器及处理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>传入的服务集合。</returns>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services)
    {
        var allAssemblyFinder = services.GetOrAddAllAssemblyFinder();
        return services.AddDomainEventDispatcher(allAssemblyFinder.FindAll());
    }

    /// <summary>
    /// 从指定程序集注册领域事件调度器及处理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="assemblies">要扫描的程序集集合。</param>
    /// <returns>传入的服务集合。</returns>
    public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services, Assembly[] assemblies)
    {
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.TryAddScoped<IDomainHandlerFactory, DependencyInjectionHandlerFactory>();
        services.RegisterEventHandler(assemblies);
        return services;
    }

    /// <summary>
    /// 扫描程序集并注册其中的领域事件处理器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="assemblies">要扫描的程序集集合。</param>
    /// <exception cref="BingFrameworkException">单个处理器实现多个领域事件处理器接口时抛出。</exception>
    private static void RegisterEventHandler(this IServiceCollection services, params Assembly[] assemblies)
    {
        var store = new DomainEventHandlerTypeStore();
        var types = assemblies.SelectMany(x => x.GetTypes());

        foreach (var type in types)
        {
            var interfaces = type.GetInterfaces();
            var handlerTypes = interfaces.Where(@interface => @interface.IsGenericType)
                .Where(@interface => _eventHandlerBaseType == @interface.GetGenericTypeDefinition())
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
