using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// <see cref="ServiceLifetime.Scoped"/>服务解析器
/// </summary>
public interface IScopedServiceResolver
{
    /// <summary>
    /// 是否可解析
    /// </summary>
    bool ResolveEnabled { get; }

    /// <summary>
    /// <see cref="ServiceLifetime.Scoped"/>生命周期的服务提供程序
    /// </summary>
    IServiceProvider ScopedProvider { get; }

    /// <summary>
    /// 获取指定服务类型的实例
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>解析到的指定服务实例；未注册时返回 <see langword="null"/>。</returns>
    T GetService<T>();

    /// <summary>
    /// 获取指定服务类型的实例
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>解析到的指定服务实例；未注册时返回 <see langword="null"/>。</returns>
    object GetService(Type serviceType);

    /// <summary>
    /// 获取指定服务类型的所有实例
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>指定服务类型的全部已注册实例。</returns>
    IEnumerable<T> GetServices<T>();

    /// <summary>
    /// 获取指定服务类型的所有实例
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>指定服务类型的全部已注册实例。</returns>
    IEnumerable<object> GetServices(Type serviceType);
}
