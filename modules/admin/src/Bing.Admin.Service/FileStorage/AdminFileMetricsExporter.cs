using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Text;
using Bing.Biz.FileManagement;
using Bing.FileStorage;

namespace Bing.Admin.Service.FileStorage;

/// <summary>导出文件队列快照及本进程 Meter。启动时创建，避免首次抓取前丢失累计计数。</summary>
public sealed class AdminFileMetricsExporter : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly object _gate = new();
    private readonly Dictionary<(string Name, string Label), Sample> _samples = new();
    private readonly HashSet<string> _stores;
    private static readonly double[] Bounds = { .1, .5, 1, 5, 15, 60, 300, double.PositiveInfinity };
    private sealed class Sample { public double Sum; public long Count; public long[] Buckets = new long[Bounds.Length]; }

    /// <summary>监听固定指标，并以已配置的存储实例限定标签集合。</summary>
    public AdminFileMetricsExporter(IEnumerable<string> stores)
    {
        _stores = new HashSet<string>(stores, StringComparer.Ordinal);
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Bing.FileManagement" && instrument.Name is
                "bing.files.operations.completed" or "bing.files.operations.failed" or "bing.files.bytes" or "bing.files.operations.duration")
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, t, _) => Record(i, v, t));
        _listener.SetMeasurementEventCallback<double>((i, v, t, _) => Record(i, v, t));
        _listener.Start();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        if (!double.IsFinite(value) || value < 0) return;
        var key = instrument.Name == "bing.files.bytes" ? "store" : instrument.Name == "bing.files.operations.failed" ? "code" : "kind";
        var label = "other";
        foreach (var tag in tags)
            if (tag.Key == key && tag.Value is string text)
            {
                // kind/code 为固定集合；store 仅接受启动配置，不允许文件或租户 ID 成为标签。
                if (key == "store" ? _stores.Contains(text) : key == "kind" ? Enum.TryParse<FileOperationKind>(text, out var kind) && Enum.IsDefined(typeof(FileOperationKind), kind) && kind.ToString() == text
                    : IsKnownError(text)) label = text;
            }
        lock (_gate)
        {
            if (!_samples.TryGetValue((instrument.Name, label), out var sample))
                _samples.Add((instrument.Name, label), sample = new Sample());
            sample.Sum += value;
            sample.Count++;
            if (instrument.Name == "bing.files.operations.duration")
                for (var index = 0; index < Bounds.Length; index++) if (value <= Bounds[index]) sample.Buckets[index]++;
        }
    }

    /// <summary>数据库快照是全局值，多副本取 max；计数器与直方图为进程值，可按实例聚合。</summary>
    public string Export(FileOperationalSnapshot snapshot)
    {
        var text = new StringBuilder();
        Gauge("bing_files_queue_pending", snapshot.PendingOperations);
        Gauge("bing_files_queue_running", snapshot.RunningOperations);
        Gauge("bing_files_queue_failed", snapshot.FailedOperations);
        Gauge("bing_files_queue_oldest_wait_seconds", snapshot.OldestDueAt == 0 ? 0 : Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - snapshot.OldestDueAt) / 1000d);
        Gauge("bing_files_io_active", snapshot.ActiveIoLeases);
        Gauge("bing_files_upload_reserved_bytes", snapshot.ReservedBytes);
        Gauge("bing_files_events_pending", snapshot.PendingEvents);
        lock (_gate)
        {
            foreach (var instrument in _samples.GroupBy(pair => pair.Key.Name).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var histogram = instrument.Key == "bing.files.operations.duration";
                var name = instrument.Key.Replace('.', '_') + (histogram ? "_seconds" : "_total");
                text.Append("# TYPE ").Append(name).Append(histogram ? " histogram\n" : " counter\n");
                var tag = instrument.Key == "bing.files.bytes" ? "store" : instrument.Key == "bing.files.operations.failed" ? "code" : "kind";
                foreach (var pair in instrument.OrderBy(pair => pair.Key.Label, StringComparer.Ordinal))
                {
                    var labels = tag + "=\"" + Escape(pair.Key.Label) + "\"";
                    if (!histogram) Line(name, labels, pair.Value.Sum);
                    else
                    {
                        Line(name + "_sum", labels, pair.Value.Sum);
                        Line(name + "_count", labels, pair.Value.Count);
                        for (var index = 0; index < Bounds.Length; index++)
                            Line(name + "_bucket", labels + ",le=\"" + (double.IsPositiveInfinity(Bounds[index]) ? "+Inf" : Number(Bounds[index])) + "\"", pair.Value.Buckets[index]);
                    }
                }
            }
        }
        return text.ToString();
        void Gauge(string name, double value) { text.Append("# TYPE ").Append(name).Append(" gauge\n").Append(name).Append(' ').Append(Number(value)).Append('\n'); }
        void Line(string name, string labels, double value) => text.Append(name).Append('{').Append(labels).Append("} ").Append(Number(value)).Append('\n');
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    private static bool IsKnownError(string value) => value.StartsWith("storage:", StringComparison.Ordinal)
        ? Enum.TryParse<FileStorageError>(value.Substring(8), out var error) && Enum.IsDefined(typeof(FileStorageError), error) && "storage:" + error == value
        : value is "files:conflict" or "files:not_found" or "files:content_rejected" or "files:configuration" or "files:capacity"
            or "IOException" or "SqliteException" or "MySqlException" or "InvalidOperationException" or "TimeoutException";
    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();
}
