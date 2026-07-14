using System.Net;
using Bing.Tracing;

namespace Bing.Logging;

/// <summary>
/// 日志上下文访问器
/// </summary>
public class LogContextAccessor : ILogContextAccessor
{
    /// <summary>
    /// 当前作用域
    /// </summary>
    private static readonly AsyncLocal<ScopeNode> CurrentScope = new();

    /// <summary>
    /// 关联标识提供程序
    /// </summary>
    private readonly ICorrelationIdProvider _correlationIdProvider;

    /// <summary>
    /// 初始化一个<see cref="LogContextAccessor"/>类型的实例
    /// </summary>
    public LogContextAccessor(ICorrelationIdProvider correlationIdProvider) =>
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));

    /// <inheritdoc />
    public LogContextSnapshot Current
    {
        get
        {
            var current = CurrentScope.Value?.Snapshot ?? Create();
            var traceId = _correlationIdProvider.Get();
            return current.TraceId == traceId ? current : current.WithTraceId(traceId);
        }
    }

    /// <summary>
    /// 创建日志上下文
    /// </summary>
    protected virtual LogContextSnapshot Create() => new(
        _correlationIdProvider.Get(),
        client: new LogClientContext(host: Dns.GetHostName()));

    /// <inheritdoc />
    public virtual LogContextSnapshot Capture() => Current;

    /// <inheritdoc />
    public virtual IDisposable BeginScope(LogContextSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        var parent = CurrentScope.Value;
        var node = new ScopeNode(snapshot, parent);
        var correlationScope = _correlationIdProvider.Change(snapshot.TraceId);
        CurrentScope.Value = node;
        return new DisposeAction(() =>
        {
            if (ReferenceEquals(CurrentScope.Value, node))
                CurrentScope.Value = parent;
            correlationScope.Dispose();
        });
    }

    /// <summary>
    /// 日志上下文作用域节点
    /// </summary>
    private sealed class ScopeNode
    {
        public ScopeNode(LogContextSnapshot snapshot, ScopeNode parent)
        {
            Snapshot = snapshot;
            Parent = parent;
        }

        public LogContextSnapshot Snapshot { get; }

        public ScopeNode Parent { get; }
    }
}
