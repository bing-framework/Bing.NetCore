using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 表示供作用域内复用的缓存服务提供程序。
/// </summary>
/// <remarks>默认实现 <see cref="CachedServiceProvider"/> 为 Scoped；其缓存仅属于当前提供器实例，可缓存 Transient 和 Scoped 服务的首次解析结果。</remarks>
public interface ICachedServiceProvider : ICachedServiceProviderBase
{
}
