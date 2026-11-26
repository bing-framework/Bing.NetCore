using System.Diagnostics.Tracing;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>每次正式测量仅写入首尾两个事件，不在请求热路径埋点。</summary>
[EventSource(Name = "Bing-Sql-MixedCapacity")]
internal sealed class SqlMixedCapacityEvents : EventSource
{
    internal static readonly SqlMixedCapacityEvents Log = new();
    [Event(1, Level = EventLevel.Informational)]
    public void MeasurementStart() => WriteEvent(1);
    [Event(2, Level = EventLevel.Informational)]
    public void MeasurementStop() => WriteEvent(2);
}
