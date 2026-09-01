using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tracing;

/// <summary>
/// 提供当前异步执行流的关联标识。
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class DefaultCorrelationIdProvider : ICorrelationIdProvider
{
    /// <summary>
    /// 保存当前异步执行流的关联标识。
    /// </summary>
    private readonly AsyncLocal<string> _currentCorrelationId = new();

    /// <summary>
    /// 获取当前关联标识。
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
