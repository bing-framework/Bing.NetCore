using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 瞬时缓存服务提供程序，缓存包含了 <see cref="ServiceLifetime.Transient"/>。<br />
/// 该服务的生命周期为 <see cref="ServiceLifetime.Transient"/>。
/// </summary>
public class TransientCachedServiceProvider : CachedServiceProviderBase, ITransientCachedServiceProvider, ITransientDependency
{
    /// <summary>
    /// 初始化一个<see cref="TransientCachedServiceProvider"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public TransientCachedServiceProvider(IServiceProvider serviceProvider) 
        : base(serviceProvider)
    {
    }
}
