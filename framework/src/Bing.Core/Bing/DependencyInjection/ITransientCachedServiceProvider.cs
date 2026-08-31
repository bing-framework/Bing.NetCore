using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 表示仅在当前提供器实例内缓存服务的瞬态服务提供程序。
/// </summary>
/// <remarks>默认实现 <see cref="TransientCachedServiceProvider"/> 为 Transient；不同实例之间不共享缓存。</remarks>
public interface ITransientCachedServiceProvider:ICachedServiceProviderBase
{
}
