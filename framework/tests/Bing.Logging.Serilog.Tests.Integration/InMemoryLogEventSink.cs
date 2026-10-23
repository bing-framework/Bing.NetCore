using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Bing.Logging.Tests;

/// <summary>
/// 保存测试宿主输出的 Serilog 事件。
/// </summary>
public sealed class InMemoryLogEventSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    /// <summary>
    /// 获取当前已记录的事件快照。
    /// </summary>
    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
