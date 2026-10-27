namespace Bing.Resilience;

/// <summary>宿主级熔断状态。安全状态、探测租约与采样不会因引擎切换丢失。</summary>
public sealed class ResilienceCircuitResources(TimeProvider clock) : IResilienceCircuitResources
{
    private readonly object _gate = new();
    private readonly Dictionary<ResiliencePipelineIdentity, Circuit> _circuits = new();
    public IResilienceSharedCircuit GetCircuit(ResiliencePipelineIdentity identity, ResilienceCircuitConfiguration configuration, Action<string> reportEvent)
    {
        lock (_gate)
        {
            if (!_circuits.TryGetValue(identity, out var circuit))
                _circuits.Add(identity, circuit = new(identity, configuration, clock));
            else if (circuit.Configuration != configuration)
                throw new ResilienceConfigurationException("共享熔断参数不能在有状态切换中更改。");
            // 事件属于当前尝试的 Provider；不能把首代回调永久保存在共享状态中。
            return new Binding(circuit, reportEvent);
        }
    }
    public ResilienceCircuitSnapshot GetSnapshot(ResiliencePipelineIdentity identity)
    {
        lock (_gate) return _circuits.TryGetValue(identity, out var circuit) ? circuit.Snapshot()
            : throw new ResilienceConfigurationException("管线没有可查询的共享熔断状态。");
    }
    /// <summary>调用方保证没有快照或尝试引用，且状态保留期已结束。</summary>
    public void Retire(ResiliencePipelineIdentity identity) { lock (_gate) _circuits.Remove(identity); }
    private sealed class Binding(Circuit circuit, Action<string> report) : IResilienceSharedCircuit
    {
        public ResilienceCircuitState State => circuit.State;
        public IResilienceCircuitLease Enter() => circuit.Enter(report);
        public ValueTask IsolateAsync(CancellationToken token) => circuit.IsolateAsync(report, token);
        public ValueTask CloseAsync(CancellationToken token) => circuit.CloseAsync(report, token);
    }
    private sealed class Circuit(ResiliencePipelineIdentity identity, ResilienceCircuitConfiguration configuration, TimeProvider clock)
    {
        private readonly object _gate = new();
        private readonly Queue<(long Time, bool Failure)> _samples = new();
        private readonly string _configurationVersion = Guid.NewGuid().ToString("N");
        private ResilienceCircuitState _state;
        private long _opened, _epoch;
        private DateTimeOffset _openedAt;
        private int _failures;
        public ResilienceCircuitConfiguration Configuration => configuration;
        public ResilienceCircuitState State { get { lock (_gate) return _state; } }
        public ResilienceCircuitSnapshot Snapshot()
        {
            lock (_gate)
            {
                Prune(clock.GetTimestamp());
                return new(identity, _state, clock.GetUtcNow(), _state == ResilienceCircuitState.Open ? _openedAt + configuration.BreakDuration : null,
                    _epoch, _configurationVersion, _samples.Count, _failures);
            }
        }
        public IResilienceCircuitLease Enter(Action<string> report)
        {
            lock (_gate)
            {
                if (_state == ResilienceCircuitState.Open && clock.GetElapsedTime(_opened) >= configuration.BreakDuration)
                {
                    _state = ResilienceCircuitState.HalfOpen; _epoch++; report("circuit.half_open"); return new Lease(this, _epoch, report);
                }
                if (_state != ResilienceCircuitState.Closed) throw new ResilienceCircuitOpenException(identity);
                return new Lease(this, _epoch, report);
            }
        }
        private void Prune(long now)
        {
            while (_samples.TryPeek(out var sample) && clock.GetElapsedTime(sample.Time, now) > configuration.SamplingDuration)
            { if (_samples.Dequeue().Failure) _failures--; }
        }
        private void Complete(long epoch, bool failure, bool excluded, Action<string> report)
        {
            lock (_gate)
            {
                if (epoch != _epoch) return;
                if (_state == ResilienceCircuitState.HalfOpen)
                {
                    if (excluded || failure) Open(report); else Reset(report);
                    return;
                }
                if (_state != ResilienceCircuitState.Closed || excluded) return;
                var now = clock.GetTimestamp();
                Prune(now);
                _samples.Enqueue((now, failure)); if (failure) _failures++;
                if (_samples.Count >= configuration.MinimumThroughput && (double)_failures / _samples.Count >= configuration.FailureRatio) Open(report);
            }
        }
        private void Open(Action<string> report) { _state = ResilienceCircuitState.Open; _epoch++; _opened = clock.GetTimestamp(); _openedAt = clock.GetUtcNow(); report("circuit.open"); }
        private void Reset(Action<string> report) { _state = ResilienceCircuitState.Closed; _epoch++; _samples.Clear(); _failures = 0; report("circuit.close"); }
        public ValueTask IsolateAsync(Action<string> report, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate) { _state = ResilienceCircuitState.Isolated; _epoch++; report("circuit.isolate"); }
            return default;
        }
        public ValueTask CloseAsync(Action<string> report, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); lock (_gate) Reset(report); return default;
        }
        private sealed class Lease(Circuit circuit, long epoch, Action<string> report) : IResilienceCircuitLease
        {
            private Circuit? _circuit = circuit;
            public void Complete(bool failure, bool excluded = false) => Interlocked.Exchange(ref _circuit, null)?.Complete(epoch, failure, excluded, report);
            public void Dispose() => Complete(false, true);
        }
    }
}
