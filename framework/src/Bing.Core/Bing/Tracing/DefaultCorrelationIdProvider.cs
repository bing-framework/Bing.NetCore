using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tracing;

/// <summary>
/// 默认跟踪标识提供程序
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class DefaultCorrelationIdProvider : ICorrelationIdProvider
{
    /// <summary>
    /// 跟踪标识提供程序
    /// </summary>
    public string Get() => CreateNewCorrelationId();

    /// <summary>
    /// 创建新跟踪标识
    /// </summary>
    protected virtual string CreateNewCorrelationId() => Guid.NewGuid().ToString("N");
}
