using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 采集 Npgsql 的进程级忙闲连接计数；不把采样峰值或开连接时间解释为池排队。
/// 每个测量窗口使用一个实例，只保留固定大小的汇总状态。
/// </summary>
internal sealed class SqlPoolCounterListener : EventListener
{
    private readonly object _sync = new();
    private Counter _busy = new(0, null, null);
    private Counter _idle = new(0, null, null);
    private volatile bool _sourceDetected;
    private bool _stopped;

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name != "Npgsql") return;
        _sourceDetected = true;
        // 只启用计数器所需级别，避免额外订阅逐命令 Informational/Verbose 事件。
        EnableEvents(source, EventLevel.LogAlways, EventKeywords.None,
            new Dictionary<string, string> { ["EventCounterIntervalSec"] = "1" });
    }

    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        if (data.EventName == "EventCounters" && data.Payload?.Count > 0 &&
            data.Payload[0] is IDictionary<string, object> payload)
            Record(payload);
    }

    internal void Record(IDictionary<string, object> payload)
    {
        if (!payload.TryGetValue("Name", out var name) || name is not string counterName ||
            counterName is not ("busy-connections" or "idle-connections") ||
            !payload.TryGetValue("Mean", out var value) || value is not double mean ||
            !double.IsFinite(mean) || mean < 0) return;

        lock (_sync)
        {
            if (_stopped) return;
            if (counterName == "busy-connections") _busy = Add(_busy, mean);
            else _idle = Add(_idle, mean);
        }
    }

    internal Result Snapshot()
    {
        lock (_sync)
        {
            var status = _busy.SampleCount > 0 && _idle.SampleCount > 0 ? "OBSERVED" :
                _busy.SampleCount > 0 || _idle.SampleCount > 0 ? "PARTIAL" : "NOT_OBSERVED";
            return new Result(status, "Npgsql", "process-wide-all-pools", 1, _sourceDetected, _busy, _idle);
        }
    }

    internal static QueueCapability CreateQueueCapability(string provider, string driver, Version driverVersion)
    {
        var knownUnavailable = driver == "Npgsql" && driverVersion is { Major: 6, Minor: 0 };
        return new QueueCapability(provider, driver, driverVersion?.ToString() ?? "unknown",
            knownUnavailable ? "NOT_INSTRUMENTED" : "UNKNOWN_CAPABILITY",
            knownUnavailable ? "DRIVER_QUEUE_TIMING_UNAVAILABLE" : "DRIVER_QUEUE_TIMING_UNVERIFIED",
            false,
            "Pure pool queue duration is not measured. Process-wide occupancy samples are reported separately in measurement.connectionPool.");
    }

    // 先关闭接收再取消订阅，排除释放过程中已经派发的回调。
    public override void Dispose()
    {
        lock (_sync) _stopped = true;
        base.Dispose();
    }

    private static Counter Add(Counter counter, double value) => new(counter.SampleCount + 1,
        counter.SampledPeak.HasValue ? Math.Max(counter.SampledPeak.Value, value) : value, value);

    internal sealed record Counter(long SampleCount, double? SampledPeak, double? Last);
    internal sealed record QueueCapability(string Provider, string Driver, string DriverVersion, string Status,
        string ReasonCode, bool OccupancyIsQueueDuration, string Detail);
    internal sealed record Result(string Status, string EventSource, string Scope, int IntervalSeconds,
        bool SourceDetected, Counter BusyConnections, Counter IdleConnections)
    {
        public string ObservationWindow => "measure-through-drain";
    }
}
