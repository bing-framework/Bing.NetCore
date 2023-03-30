using System.Collections.Concurrent;

namespace Bing.DependencyInjection;

/// <summary>
/// 缓存服务提供程序基类
/// </summary>
public abstract class CachedServiceProviderBase : ICachedServiceProviderBase
{
    /// <summary>
    /// 初始化一个<see cref="CachedServiceProviderBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    protected CachedServiceProviderBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        CacheServices = new ConcurrentDictionary<Type, Lazy<object>>();
        //CacheServices.TryAdd(typeof(IServiceProvider), new Lazy<object>(() => ServiceProvider));
    }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 缓存服务字典
    /// </summary>
    protected ConcurrentDictionary<Type, Lazy<object>> CacheServices { get; }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    public object GetService(Type serviceType)
    {
        return CacheServices.GetOrAdd(serviceType, _ => new Lazy<object>(() => ServiceProvider.GetService(serviceType))).Value;
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="defaultValue">默认服务</param>
    public T GetService<T>(T defaultValue)
    {
        return (T)GetService(typeof(T), defaultValue);
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="defaultValue">默认服务</param>
    public object GetService(Type serviceType, object defaultValue)
    {
        return GetService(serviceType) ?? defaultValue;
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="factory">服务实例工厂</param>
    public T GetService<T>(Func<IServiceProvider, object> factory)
    {
        return (T)GetService(typeof(T), factory);
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="factory">服务实例工厂</param>
    public object GetService(Type serviceType, Func<IServiceProvider, object> factory)
    {
        return CacheServices.GetOrAdd(serviceType, _ => new System.Lazy<object>(() => factory(ServiceProvider))).Value;
    }
}
