using System.Diagnostics.Metrics;

namespace Bing.Biz.FileManagement;

/// <summary>每宿主采样全局数据库快照；跨宿主聚合应取 max，不能对全局计数求和。</summary>
public sealed class FileOperationalMetrics : IDisposable
{
    private readonly Meter _meter = new("Bing.FileManagement.Operations", "1.0");
    private FileOperationalSnapshot _snapshot = new();
    public FileOperationalMetrics()
    {
        _meter.CreateObservableGauge("bing.files.queue.pending", () => Volatile.Read(ref _snapshot).PendingOperations);
        _meter.CreateObservableGauge("bing.files.queue.running", () => Volatile.Read(ref _snapshot).RunningOperations);
        _meter.CreateObservableGauge("bing.files.queue.failed", () => Volatile.Read(ref _snapshot).FailedOperations);
        _meter.CreateObservableGauge("bing.files.queue.oldest_wait", () =>
        { var due = Volatile.Read(ref _snapshot).OldestDueAt; return due == 0 ? 0d : Math.Max(0, FileManagementService.Now - due) / 1000d; }, "s");
        _meter.CreateObservableGauge("bing.files.io.active", () => Volatile.Read(ref _snapshot).ActiveIoLeases);
        _meter.CreateObservableGauge("bing.files.upload.reserved", () => Volatile.Read(ref _snapshot).ReservedBytes, "By");
        _meter.CreateObservableGauge("bing.files.events.pending", () => Volatile.Read(ref _snapshot).PendingEvents);
    }
    public void Update(FileOperationalSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);
    public void Dispose() => _meter.Dispose();
}
