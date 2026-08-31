using System.Collections.Concurrent;

namespace Bing.DependencyInjection;

/// <summary>
/// 按服务类型缓存依赖注入解析结果的基类。
/// </summary>
public abstract class CachedServiceProviderBase : ICachedServiceProviderBase
{
    /// <summary>
    /// 使用服务提供程序初始化 <see cref="CachedServiceProviderBase"/> 的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析服务的服务提供程序。</param>
    protected CachedServiceProviderBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        CacheServices = new ConcurrentDictionary<Type, Lazy<object>>();
        //CacheServices.TryAdd(typeof(IServiceProvider), new Lazy<object>(() => ServiceProvider));
    }

    /// <summary>
    /// 获取用于解析服务的底层服务提供程序。
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取以服务类型为键的延迟解析缓存。
    /// </summary>
    protected ConcurrentDictionary<Type, Lazy<object>> CacheServices { get; }

    /// <summary>
    /// 从底层服务提供程序获取并缓存指定类型的服务。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <returns>首次解析后缓存的服务实例；服务未注册时可能为 <c>null</c>。</returns>
    /// <remarks>同一服务类型的解析委托仅执行一次，包含未注册服务返回的 <c>null</c> 结果。</remarks>
    public object GetService(Type serviceType)
    {
        return CacheServices.GetOrAdd(serviceType, _ => new Lazy<object>(() => ServiceProvider.GetService(serviceType))).Value;
    }

    /// <inheritdoc />
    public T GetService<T>(T defaultValue)
    {
        return (T)GetService(typeof(T), defaultValue);
    }

    /// <inheritdoc />
    public object GetService(Type serviceType, object defaultValue)
    {
        return GetService(serviceType) ?? defaultValue;
    }

    /// <inheritdoc />
    public T GetService<T>(Func<IServiceProvider, object> factory)
    {
        return (T)GetService(typeof(T), factory);
    }

    /// <inheritdoc />
    public object GetService(Type serviceType, Func<IServiceProvider, object> factory)
    {
        return CacheServices.GetOrAdd(serviceType, _ => new System.Lazy<object>(() => factory(ServiceProvider))).Value;
    }
}
