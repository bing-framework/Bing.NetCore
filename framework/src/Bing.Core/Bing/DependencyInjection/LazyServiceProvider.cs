using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// Lazy延迟加载服务提供程序
/// </summary>
public class LazyServiceProvider : ILazyServiceProvider, ITransientDependency
{
    /// <summary>
    /// 缓存服务字典
    /// </summary>
    protected ConcurrentDictionary<Type, Lazy<object>> CachedServices { get; }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// 初始化一个<see cref="LazyServiceProvider"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public LazyServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        CachedServices = new ConcurrentDictionary<Type, Lazy<object>>();
        CachedServices.TryAdd(typeof(IServiceProvider), new Lazy<object>(() => ServiceProvider));
    }

    /// <summary>
    /// 获取请求服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    public virtual T LazyGetRequiredService<T>() => (T)LazyGetRequiredService(typeof(T));

    /// <summary>
    /// 获取请求服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    public virtual object LazyGetRequiredService(Type serviceType) => ServiceProvider.GetRequiredService(serviceType);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    public virtual T LazyGetService<T>() => (T)LazyGetService(typeof(T));

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    public virtual object LazyGetService(Type serviceType)
    {
        return CachedServices.GetOrAdd(
            serviceType,
            _ => new Lazy<object>(() => ServiceProvider.GetService(serviceType))
        ).Value;
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="defaultValue">默认服务</param>
    public virtual T LazyGetService<T>(T defaultValue) => (T)LazyGetService(typeof(T), defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="defaultValue">默认服务</param>
    public virtual object LazyGetService(Type serviceType, object defaultValue) => LazyGetService(serviceType) ?? defaultValue;

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="factory">服务实例工厂</param>
    public virtual T LazyGetService<T>(Func<IServiceProvider, object> factory)
    {
        return (T)LazyGetService(typeof(T), factory);
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="factory">服务实例工厂</param>
    public virtual object LazyGetService(Type serviceType, Func<IServiceProvider, object> factory)
    {
        return CachedServices.GetOrAdd(
            serviceType,
            _ => new Lazy<object>(() => factory(ServiceProvider))
        ).Value;
    }
}
