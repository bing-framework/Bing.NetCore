using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tracing;

/// <summary>
/// 默认关联标识生成器
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class DefaultCorrelationIdGenerator : ICorrelationIdGenerator
{
    /// <inheritdoc />
    public virtual string Create() => Guid.NewGuid().ToString("N");
}