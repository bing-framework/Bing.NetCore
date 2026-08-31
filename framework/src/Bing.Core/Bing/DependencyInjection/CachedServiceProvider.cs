using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 以 Scoped 生命周期注册的缓存服务提供程序。
/// </summary>
public class CachedServiceProvider : CachedServiceProviderBase, ICachedServiceProvider, IScopedDependency
{
    /// <summary>
    /// 使用当前服务作用域的服务提供程序初始化 <see cref="CachedServiceProvider"/> 的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析并缓存服务的服务提供程序。</param>
    public CachedServiceProvider(IServiceProvider serviceProvider) 
        : base(serviceProvider)
    {
    }
}
