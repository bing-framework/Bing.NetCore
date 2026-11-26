/// <summary>单次探针窗口；缺失或重复标记时不把整段进程采样冒充测量样本。</summary>
internal sealed class CpuMeasurementWindow(int processId, bool required)
{
    internal const string ProviderName = "Bing-Sql-MixedCapacity";
    private int _starts, _stops;
    internal double? StartMilliseconds { get; private set; }
    internal double? StopMilliseconds { get; private set; }
    private bool HasMarkers => _starts != 0 || _stops != 0;
    internal string Scope => HasMarkers || required ? "measure-through-drain" : "entire-process";
    internal bool IsValid => !HasMarkers && !required ||
        _starts == 1 && _stops == 1 && StartMilliseconds is double start &&
        StopMilliseconds is double stop && double.IsFinite(start) && double.IsFinite(stop) && start >= 0 && stop > start;
    internal void AddMarker(int pid, int eventId, double milliseconds)
    {
        if (pid != processId) return;
        if (eventId == 1) { _starts++; StartMilliseconds = milliseconds; }
        if (eventId == 2) { _stops++; StopMilliseconds = milliseconds; }
    }
    internal bool Contains(double milliseconds) => IsValid &&
        (!HasMarkers || milliseconds >= StartMilliseconds && milliseconds < StopMilliseconds);
}
