namespace Bing.Resilience;

/// <summary>按登记顺序准入、逆序释放；上报失败记录事件，不覆盖已确定的业务结果。</summary>
public sealed class ResilienceAttemptObservations<T> : IAsyncDisposable
{
    private readonly List<IResilienceAttemptObservation<T>> _leases = new();
    private readonly Action<string> _reportEvent;
    private bool _completed;
    private ResilienceAttemptObservations(Action<string> reportEvent) => _reportEvent = reportEvent;
    public static async ValueTask<ResilienceAttemptObservations<T>> BeginAsync(IEnumerable<IResilienceAttemptObserver<T>> observers,
        ResilienceAttemptContext context, Action<string> reportEvent, CancellationToken token)
    {
        var group = new ResilienceAttemptObservations<T>(reportEvent);
        try
        {
            foreach (var observer in observers) group._leases.Add(await observer.BeginAsync(context, token).ConfigureAwait(false));
            return group;
        }
        catch { await group.DisposeAsync().ConfigureAwait(false); throw; }
    }
    public async ValueTask CompleteAsync(ResilienceOutcome<T> outcome, CancellationToken token)
    {
        if (_completed) return; _completed = true;
        foreach (var observation in _leases.AsEnumerable().Reverse())
            try { await observation.CompleteAsync(outcome, token).ConfigureAwait(false); }
            catch { _reportEvent("attempt.report_failed"); }
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var observation in _leases.AsEnumerable().Reverse())
            try { await observation.DisposeAsync().ConfigureAwait(false); }
            catch { _reportEvent("attempt.release_failed"); }
        _leases.Clear();
    }
}
