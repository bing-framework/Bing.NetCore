using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 瞬时缓存服务提供程序，缓存包含了 <see cref="ServiceLifetime.Transient"/>。<br />
/// 该服务的生命周期为 <see cref="ServiceLifetime.Transient"/>。
/// </summary>
public interface ITransientCachedServiceProvider:ICachedServiceProviderBase
{
    
}
