using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 缓存服务提供程序，缓存包含了 <see cref="ServiceLifetime.Transient"/>、<see cref="ServiceLifetime.Scoped"/>。<br />
/// 该服务的生命周期为 <see cref="ServiceLifetime.Scoped"/>。
/// </summary>
public class CachedServiceProvider : CachedServiceProviderBase, ICachedServiceProvider, IScopedDependency
{
    /// <summary>
    /// 初始化一个<see cref="CachedServiceProvider"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public CachedServiceProvider(IServiceProvider serviceProvider) 
        : base(serviceProvider)
    {
    }
}
