using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 使用服务提供程序延迟解析并缓存可选服务的默认实现。
/// </summary>
public class LazyServiceProvider : ILazyServiceProvider, ITransientDependency
{
    /// <summary>
    /// 获取以服务类型为键的延迟解析缓存。
    /// </summary>
    protected ConcurrentDictionary<Type, Lazy<object>> CachedServices { get; }

    /// <summary>
    /// 获取用于解析服务的底层服务提供程序。
    /// </summary>
    protected IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// 使用服务提供程序初始化 <see cref="LazyServiceProvider"/> 的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析服务的服务提供程序。</param>
    public LazyServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        CachedServices = new ConcurrentDictionary<Type, Lazy<object>>();
        CachedServices.TryAdd(typeof(IServiceProvider), new Lazy<object>(() => ServiceProvider));
    }

    /// <inheritdoc />
    public virtual T LazyGetRequiredService<T>() => (T)LazyGetRequiredService(typeof(T));

    /// <inheritdoc />
    public virtual object LazyGetRequiredService(Type serviceType) => ServiceProvider.GetRequiredService(serviceType);

    /// <inheritdoc />
    public virtual T LazyGetService<T>() => (T)LazyGetService(typeof(T));

    /// <inheritdoc />
    /// <remarks>首次解析结果会按服务类型缓存，包含未注册服务的 <c>null</c> 结果。</remarks>
    public virtual object LazyGetService(Type serviceType)
    {
        return CachedServices.GetOrAdd(
            serviceType,
            _ => new Lazy<object>(() => ServiceProvider.GetService(serviceType))
        ).Value;
    }

    /// <inheritdoc />
    public virtual T LazyGetService<T>(T defaultValue) => (T)LazyGetService(typeof(T), defaultValue);

    /// <inheritdoc />
    public virtual object LazyGetService(Type serviceType, object defaultValue) => LazyGetService(serviceType) ?? defaultValue;

    /// <inheritdoc />
    public virtual T LazyGetService<T>(Func<IServiceProvider, object> factory)
    {
        return (T)LazyGetService(typeof(T), factory);
    }

    /// <inheritdoc />
    /// <remarks>服务类型尚未存在缓存时调用 <paramref name="factory"/>；已有缓存时不会调用该工厂。</remarks>
    public virtual object LazyGetService(Type serviceType, Func<IServiceProvider, object> factory)
    {
        return CachedServices.GetOrAdd(
            serviceType,
            _ => new Lazy<object>(() => factory(ServiceProvider))
        ).Value;
    }
}
