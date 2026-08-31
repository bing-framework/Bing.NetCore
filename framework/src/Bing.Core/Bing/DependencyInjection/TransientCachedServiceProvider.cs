using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 以 Transient 生命周期注册的缓存服务提供程序。
/// </summary>
public class TransientCachedServiceProvider : CachedServiceProviderBase, ITransientCachedServiceProvider, ITransientDependency
{
    /// <summary>
    /// 使用服务提供程序初始化 <see cref="TransientCachedServiceProvider"/> 的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析并缓存服务的服务提供程序。</param>
    public TransientCachedServiceProvider(IServiceProvider serviceProvider) 
        : base(serviceProvider)
    {
    }
}
