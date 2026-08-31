using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务集合 - 通用 扩展
/// </summary>
public static class ServiceCollectionCommonExtensions
{
    /// <summary>
    /// 是否已注册指定类型
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合中存在指定服务类型注册时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool IsAdded<T>(this IServiceCollection services) => services.IsAdded(typeof(T));

    /// <summary>
    /// 是否已注册指定类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">类型</param>
    /// <returns>服务集合中存在指定服务类型注册时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool IsAdded(this IServiceCollection services, Type type) => services.Any(x => x.ServiceType == type);

    /// <summary>
    /// 替换服务
    /// </summary>
    /// <typeparam name="TService">服务定义类型</typeparam>
    /// <typeparam name="TImplement">服务实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="lifetime">生命周期</param>
    /// <returns>完成服务替换后的服务集合。</returns>
    public static IServiceCollection Replace<TService, TImplement>(this IServiceCollection services, ServiceLifetime lifetime)
    {
        var descriptor = new ServiceDescriptor(typeof(TService), typeof(TImplement), lifetime);
        services.Replace(descriptor);
        return services;
    }

    /// <summary>
    /// 获取单例注册服务对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>已注册的单例服务对象；未找到时返回默认值。</returns>
    public static T GetSingletonInstanceOrNull<T>(this IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(T) && x.Lifetime == ServiceLifetime.Singleton);
        if (descriptor?.ImplementationInstance != null)
            return (T)descriptor.ImplementationInstance;
        if (descriptor?.ImplementationFactory != null)
            return (T)descriptor.ImplementationFactory.Invoke(null);
        return default;
    }

    /// <summary>
    /// 获取单例注册服务对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>已注册的单例服务对象。</returns>
    /// <exception cref="InvalidOperationException">未找到指定类型的单例服务时抛出。</exception>
    public static T GetSingletonInstance<T>(this IServiceCollection services)
    {
        var instance = services.GetSingletonInstanceOrNull<T>();
        if (instance == null)
            throw new InvalidOperationException($"无法找到已注册的单例服务: {typeof(T).AssemblyQualifiedName}");
        return instance;
    }

    /// <summary>
    /// 如果指定服务不存在，则添加指定服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="toAddDescriptor">服务描述</param>
    /// <returns>已存在或新添加的服务描述。</returns>
    public static ServiceDescriptor GetOrAdd(this IServiceCollection services, ServiceDescriptor toAddDescriptor)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == toAddDescriptor.ServiceType);
        if (descriptor != null)
            return descriptor;
        services.Add(toAddDescriptor);
        return toAddDescriptor;
    }

    /// <summary>
    /// 获取或添加指定类型查找器
    /// </summary>
    /// <typeparam name="TTypeFinder">类型查找器类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="factory">实例工厂</param>
    /// <returns>已注册或新创建的类型查找器。</returns>
    public static TTypeFinder GetOrAddTypeFinder<TTypeFinder>(this IServiceCollection services,
        Func<IAllAssemblyFinder, TTypeFinder> factory) where TTypeFinder : class
    {
        return services.GetOrAddSingletonInstance<TTypeFinder>(() =>
        {
            var allAssemblyFinder = services.GetOrAddSingletonInstance<IAllAssemblyFinder>(() => new AppDomainAllAssemblyFinder());
            return factory(allAssemblyFinder);
        });
    }

    /// <summary>
    /// 获取或添加所有程序集查找器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>已注册或新创建的所有程序集查找器。</returns>
    public static IAllAssemblyFinder GetOrAddAllAssemblyFinder(this IServiceCollection services)
    {
        var allAssemblyFinder = services.GetOrAddSingletonInstance<IAllAssemblyFinder>(() => new AppDomainAllAssemblyFinder());
        return allAssemblyFinder;
    }

    /// <summary>
    /// 如果指定服务不存在，创建实例并添加
    /// </summary>
    /// <typeparam name="TServiceType">服务类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="factory">实例工厂</param>
    /// <returns>已注册或新创建的服务实例。</returns>
    public static TServiceType GetOrAddSingletonInstance<TServiceType>(this IServiceCollection services,
        Func<TServiceType> factory) where TServiceType : class
    {
        var item = GetSingletonInstanceOrNull<TServiceType>(services);
        if (item == null)
        {
            item = factory();
            services.AddSingleton<TServiceType>(item);
        }
        return item;
    }

    /// <summary>
    /// 批量注册服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="types">类型集合</param>
    /// <param name="serviceLifetime">服务生命周期</param>
    /// <returns>完成服务注册后的服务集合。</returns>
    public static IServiceCollection BatchRegisterService(this IServiceCollection services, Type[] types,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
    {
        var typeDict = new Dictionary<Type, Type[]>();
        foreach (var type in types)
        {
            var interfaces = type.GetInterfaces();
            typeDict.Add(type, interfaces);
        }
        if (typeDict.Keys.Count <= 0)
            return services;
        foreach (var instanceType in typeDict.Keys)
        {
            foreach (var interfaceType in typeDict[instanceType])
            {
                switch (serviceLifetime)
                {
                    case ServiceLifetime.Scoped:
                        services.AddScoped(interfaceType, instanceType);
                        break;

                    case ServiceLifetime.Singleton:
                        services.AddSingleton(interfaceType, instanceType);
                        break;

                    case ServiceLifetime.Transient:
                        services.AddTransient(interfaceType, instanceType);
                        break;
                }
            }
        }

        return services;
    }
}
