namespace Bing.Resilience;

/// <summary>持久 Worker 的尝试范围。通过执行上下文传播，HTTP 管线据此禁用内层 Retry/Hedging。</summary>
public sealed class ResilienceDurableAttemptScope : IDisposable
{
    private static readonly AsyncLocal<ResilienceDurableAttemptScope?> Slot = new();
    private readonly ResilienceDurableAttemptScope? _previous;
    private int _disposed;
    public static ResilienceDurableAttemptScope? Current => Slot.Value;
    public long? Generation { get; private set; }
    public string? Endpoint { get; private set; }
    public ResilienceDurableAttemptScope() { _previous = Slot.Value; Slot.Value = this; }
    public void ObserveGeneration(long generation) => Generation = generation;
    public void ObserveEndpoint(string endpoint) => Endpoint = endpoint;
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) Slot.Value = _previous; }
}
