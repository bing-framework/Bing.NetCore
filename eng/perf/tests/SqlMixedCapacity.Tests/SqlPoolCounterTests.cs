using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using Bing.Data.Sql.Benchmarks;

internal static class SqlPoolCounterTests
{
    internal static async Task RunAsync()
    {
        ReportsMissingCountersAsNull();
        KeepsIndependentCounterSamplesAndPeaks();
        RejectsMalformedAndUnrelatedSamples();
        SupportsConcurrentCallbacksAndSnapshots();
        KeepsMeasurementInstancesIsolated();
        SerializesExplicitObservationScope();
        ReportsNpgsql60AsKnownQueueTimingUnavailable();
        LeavesOtherDriverVersionsUnknown();
        SerializesQueueCapabilitySeparatelyFromOccupancy();
        await ObservesExistingSourceAndStopsAfterDispose();
        await ObservesSourceCreatedAfterListener();
        Console.WriteLine("SQL_POOL_COUNTER_TESTS_PASS (11)");
    }

    internal static async Task RunDriverContractAsync(string assemblyPath)
    {
        // 仅测试通过实际候选驱动启动诊断源，不连接数据库，也不修改生产反射路径。
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        var sourceType = assembly.GetType("Npgsql.NpgsqlEventSource", throwOnError: true)!;
        using var listener = new SqlPoolCounterListener();
        RuntimeHelpers.RunClassConstructor(sourceType.TypeHandle);
        await WaitForSamples(listener);
        var result = listener.Snapshot();
        Check(result.SourceDetected && result.BusyConnections.Last == 0 && result.IdleConnections.Last == 0,
            "实际候选 Npgsql 的空池计数为零，并通过 EventCounters 回调采集。");
        var version = assembly.GetName().Version;
        var capability = SqlPoolCounterListener.CreateQueueCapability("PostgreSQL", "Npgsql", version);
        Check(capability.DriverVersion == version?.ToString() &&
            capability.Status == (version is { Major: 6, Minor: 0 } ? "NOT_INSTRUMENTED" : "UNKNOWN_CAPABILITY"),
            "能力报告使用候选驱动的真实 AssemblyVersion，并只对已核实的 6.0.x 分类。");
        Console.WriteLine($"SQL_POOL_DRIVER_CONTRACT_PASS ({assembly.GetName().Version})");
    }

    private static void ReportsMissingCountersAsNull()
    {
        using var listener = new SqlPoolCounterListener();
        var result = listener.Snapshot();
        Check(result.Status == "NOT_OBSERVED" && result.Scope == "process-wide-all-pools" &&
            result.IntervalSeconds == 1 && result.BusyConnections.SampleCount == 0 &&
            result.BusyConnections.SampledPeak == null && result.IdleConnections.Last == null,
            "没有样本时不将缺测伪装为零连接。");
    }

    private static void KeepsIndependentCounterSamplesAndPeaks()
    {
        using var listener = new SqlPoolCounterListener();
        listener.Record(Sample("busy-connections", 3d));
        Check(listener.Snapshot().Status == "PARTIAL", "只有一种计数时报告 PARTIAL。");
        listener.Record(Sample("idle-connections", 8d));
        listener.Record(Sample("busy-connections", 2d));
        listener.Record(Sample("idle-connections", 1d));
        var result = listener.Snapshot();
        Check(result.Status == "OBSERVED" && result.BusyConnections.SampleCount == 2 &&
            result.BusyConnections.SampledPeak == 3 && result.BusyConnections.Last == 2 &&
            result.IdleConnections.SampleCount == 2 && result.IdleConnections.SampledPeak == 8 &&
            result.IdleConnections.Last == 1,
            "忙闲计数分别记录采样峰值和最后值，不推导同时连接总量。");
    }

    private static void RejectsMalformedAndUnrelatedSamples()
    {
        using var listener = new SqlPoolCounterListener();
        foreach (var sample in new[]
        {
            Sample("unrelated", 12d), Sample("busy-connections", -1d),
            Sample("busy-connections", double.NaN), Sample("busy-connections", double.PositiveInfinity),
            Sample("busy-connections", "2"), new Dictionary<string, object> { ["Name"] = "busy-connections" },
            new Dictionary<string, object> { ["Mean"] = 2d }
        }) listener.Record(sample);
        Check(listener.Snapshot().Status == "NOT_OBSERVED", "未知计数及非法 Mean 不污染报告。");
        listener.Record(Sample("busy-connections", 0d));
        Check(listener.Snapshot().BusyConnections.Last == 0, "真实零样本与缺测不同。");
    }

    private static void SupportsConcurrentCallbacksAndSnapshots()
    {
        using var listener = new SqlPoolCounterListener();
        Parallel.For(0, 1000, index =>
        {
            listener.Record(Sample("busy-connections", (double)index));
            listener.Record(Sample("idle-connections", 0d));
            var snapshot = listener.Snapshot();
            Check(snapshot.BusyConnections.SampleCount >= snapshot.IdleConnections.SampleCount,
                "快照在同一锁内读取两个计数状态。");
        });
        var result = listener.Snapshot();
        Check(result.BusyConnections.SampleCount == 1000 && result.IdleConnections.SampleCount == 1000 &&
            result.BusyConnections.SampledPeak == 999, "并发回调不丢失样本。");
    }

    private static void KeepsMeasurementInstancesIsolated()
    {
        using (var first = new SqlPoolCounterListener())
        {
            first.Record(Sample("busy-connections", 32d));
            first.Dispose();
            first.Record(Sample("busy-connections", 64d));
            Check(first.Snapshot().BusyConnections.SampleCount == 1, "释放后的在途回调不改变快照。");
        }
        using var second = new SqlPoolCounterListener();
        Check(second.Snapshot().BusyConnections.SampleCount == 0, "不同测量实例不共享统计状态。");
    }

    private static void SerializesExplicitObservationScope()
    {
        using var listener = new SqlPoolCounterListener();
        var before = listener.Snapshot();
        listener.Record(Sample("busy-connections", 32d));
        var json = JsonSerializer.Serialize(before, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Check(json == "{\"status\":\"NOT_OBSERVED\",\"eventSource\":\"Npgsql\",\"scope\":\"process-wide-all-pools\",\"intervalSeconds\":1,\"sourceDetected\":false,\"busyConnections\":{\"sampleCount\":0,\"sampledPeak\":null,\"last\":null},\"idleConnections\":{\"sampleCount\":0,\"sampledPeak\":null,\"last\":null},\"observationWindow\":\"measure-through-drain\"}",
            "JSON 显式保留观测范围和空值；已有快照不被后续回调改变。");
    }

    private static void ReportsNpgsql60AsKnownQueueTimingUnavailable()
    {
        var capability = SqlPoolCounterListener.CreateQueueCapability("PostgreSQL", "Npgsql", new Version(6, 0, 11, 0));
        Check(capability.Provider == "PostgreSQL" && capability.Driver == "Npgsql" &&
            capability.DriverVersion == "6.0.11.0" && capability.Status == "NOT_INSTRUMENTED" &&
            capability.ReasonCode == "DRIVER_QUEUE_TIMING_UNAVAILABLE" && !capability.OccupancyIsQueueDuration,
            "Npgsql 6.0.x 明确记录驱动版本、缺失原因，并声明占用计数不是队列时长。");
    }

    private static void LeavesOtherDriverVersionsUnknown()
    {
        foreach (var version in new[] { new Version(6, 1, 0), new Version(7, 0, 0) })
        {
            var capability = SqlPoolCounterListener.CreateQueueCapability("PostgreSQL", "Npgsql", version);
            Check(capability.Status == "UNKNOWN_CAPABILITY" &&
                capability.ReasonCode == "DRIVER_QUEUE_TIMING_UNVERIFIED" &&
                capability.DriverVersion == version.ToString(),
                $"未核实版本 {version} 只报告能力未知，不外推 6.0.x 结论。");
        }
    }

    private static void SerializesQueueCapabilitySeparatelyFromOccupancy()
    {
        var capability = SqlPoolCounterListener.CreateQueueCapability("PostgreSQL", "Npgsql", new Version(6, 0, 11, 0));
        var json = JsonSerializer.Serialize(capability, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Check(json == "{\"provider\":\"PostgreSQL\",\"driver\":\"Npgsql\",\"driverVersion\":\"6.0.11.0\",\"status\":\"NOT_INSTRUMENTED\",\"reasonCode\":\"DRIVER_QUEUE_TIMING_UNAVAILABLE\",\"occupancyIsQueueDuration\":false,\"detail\":\"Pure pool queue duration is not measured. Process-wide occupancy samples are reported separately in measurement.connectionPool.\"}",
            "能力报告保留 status/detail 兼容字段，并明确忙闲占用与排队时长分离。");
    }

    private static async Task ObservesExistingSourceAndStopsAfterDispose()
    {
        using var source = new PoolSource("Npgsql");
        using var listener = new SqlPoolCounterListener();
        await WaitForSamples(listener);
        var snapshot = listener.Snapshot();
        Check(snapshot.SourceDetected && snapshot.BusyConnections.Last == 4 &&
            snapshot.IdleConnections.Last == 2 && source.IntervalRequested,
            "已存在的 EventSource 通过真实 EventCounters 回调订阅。");
        listener.Dispose();
        var count = listener.Snapshot().BusyConnections.SampleCount;
        await Task.Delay(1200);
        Check(listener.Snapshot().BusyConnections.SampleCount == count, "释放后停止采集。");
    }

    private static async Task ObservesSourceCreatedAfterListener()
    {
        using var listener = new SqlPoolCounterListener();
        using var unrelated = new PoolSource("OtherSqlPool");
        using var source = new PoolSource("Npgsql");
        await WaitForSamples(listener);
        Check(listener.Snapshot().SourceDetected && !unrelated.IntervalRequested,
            "订阅后创建的 Npgsql 源会启用计数，其他源不被启用。");
    }

    private static async Task WaitForSamples(SqlPoolCounterListener listener)
    {
        var deadline = Stopwatch.StartNew();
        while (listener.Snapshot().Status != "OBSERVED" && deadline.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(20);
        Check(listener.Snapshot().Status == "OBSERVED", "五秒内收到真实 EventCounter 样本。");
    }

    private static Dictionary<string, object> Sample(string name, object value) => new()
    {
        ["Name"] = name, ["Mean"] = value
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // 使用框架的 PollingCounter 发出真实负载格式，不把手工 Record 当成订阅验证。
    private sealed class PoolSource : EventSource
    {
        private readonly PollingCounter _busy;
        private readonly PollingCounter _idle;
        internal bool IntervalRequested;

        internal PoolSource(string name) : base(name)
        {
            _busy = new PollingCounter("busy-connections", this, () => 4);
            _idle = new PollingCounter("idle-connections", this, () => 2);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable && command.Arguments != null &&
                command.Arguments.TryGetValue("EventCounterIntervalSec", out var interval) && interval == "1")
                IntervalRequested = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _busy?.Dispose(); _idle?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
