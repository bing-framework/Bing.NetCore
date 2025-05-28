using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 缓存服务提供程序，缓存包含了 <see cref="ServiceLifetime.Transient"/>、<see cref="ServiceLifetime.Scoped"/>。<br />
/// 该服务的生命周期为 <see cref="ServiceLifetime.Scoped"/>。
/// </summary>
public interface ICachedServiceProvider : ICachedServiceProviderBase
{
}
