using System;
using System.Linq;
using Bing.Core.Modularity;
using Bing.EventBus.Local;
using Bing.Extensions;
using Bing.Finders;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.EventBus.Tests;

/// <summary>
/// 事件总线模块
/// </summary>
public class EventBusModule : BingModule
{
    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.TryAddTransient<ILocalEventBus, LocalEventBus>();
        services.TryAddTransient<IEventBus, LocalEventBus>();
        var finder = services.GetOrAddTypeFinder(assemblyFinder => new LocalEventHandlerTypeFinder(assemblyFinder));
        foreach (var handlerType in finder.FindAll())
        {
            var serviceTypes =
                handlerType.FindInterfaces(
                    (filter, criteria) =>
                        criteria != null &&
                        filter.IsGenericType,
                    handlerType);
            serviceTypes.ToList().ForEach(serviceType => services.AddScoped(serviceType, handlerType));
        }
        return services;
    }
}

/// <summary>
/// 本地事件处理器 类型查找器
/// </summary>
public interface ILocalEventHandlerTypeFinder : ITypeFinder
{
}

/// <summary>
/// 本地事件处理器 类型查找器
/// </summary>
public class LocalEventHandlerTypeFinder : FinderBase<Type>, ILocalEventHandlerTypeFinder
{
    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 初始化一个<see cref="LocalEventHandlerTypeFinder"/>类型的实例
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public LocalEventHandlerTypeFinder(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

    /// <inheritdoc />
    protected override Type[] FindAllItems()
    {
        var assemblies = _allAssemblyFinder.FindAll(true);
        var types = assemblies
            .SelectMany(assembly => assembly.GetTypes().Where(type =>
                type.IsClass && 
                !type.IsAbstract && 
                !type.IsInterface &&
                typeof(ILocalEventHandler<>).IsGenericAssignableFrom(type))).ToArray();
        return types;
    }
}