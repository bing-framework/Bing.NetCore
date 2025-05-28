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
    /// 使用AsyncLocal存储当前上下文的关联ID，确保在异步调用中保持一致性。
    /// </summary>
    private readonly AsyncLocal<string> _currentCorrelationId = new();

    /// <summary>
    /// 当前关联ID
    /// </summary>
    private string CorrelationId => _currentCorrelationId.Value;

    /// <inheritdoc />
    public virtual string Get() => CorrelationId;

    /// <inheritdoc />
    public virtual IDisposable Change(string correlationId)
    {
        var parent = CorrelationId;
        _currentCorrelationId.Value = correlationId;
        return new DisposeAction(() =>
        {
            _currentCorrelationId.Value = parent;
        });
    }
}
