namespace Bing.Resilience;

/// <summary>由宿主持有的中性准入资源。时钟、队列和限额不会随 Provider 切换重置。</summary>
public sealed class ResilienceAdmissionResources(TimeProvider clock) : IResilienceAdmissionResources, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<(ResiliencePipelineIdentity, bool), Limiter> _limiters = new();
    private bool _disposed;

    public ValueTask<IDisposable> AcquireConcurrencyAsync(ResiliencePipelineIdentity identity, ResilienceConcurrencyDefinition options, CancellationToken token) =>
        Get(identity, false, options, () => new Limiter(clock, options.PermitLimit, options.QueueLimit, 0, TimeSpan.Zero)).Acquire(token);
    public ValueTask<IDisposable> AcquireRateAsync(ResiliencePipelineIdentity identity, ResilienceRateLimitDefinition options, CancellationToken token) =>
        Get(identity, true, options, () => new Limiter(clock, options.TokenLimit, options.QueueLimit, options.TokensPerPeriod, options.ReplenishmentPeriod)).Acquire(token);

    private Limiter Get(ResiliencePipelineIdentity identity, bool rate, object options, Func<Limiter> create)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ResilienceAdmissionResources));
            var key = (identity, rate);
            if (!_limiters.TryGetValue(key, out var limiter))
            {
                limiter = create(); limiter.Options = options; limiter.Identity = identity; limiter.IsRate = rate;
                _limiters.Add(key, limiter);
            }
            else if (!Equals(limiter.Options, options))
                throw new ResilienceConfigurationException("运行中的共享准入配置不能更改；请排空并重启宿主。");
            return limiter;
        }
    }
    /// <summary>仅在分区已排空且冷却结束后回收资源。</summary>
    public void Retire(ResiliencePipelineIdentity identity)
    {
        lock (_gate) foreach (var key in _limiters.Keys.Where(k => k.Item1 == identity).ToArray())
        { _limiters[key].Dispose(); _limiters.Remove(key); }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var limiter in _limiters.Values) limiter.Dispose();
            _limiters.Clear();
        }
    }

    private sealed class Limiter : IDisposable
    {
        private readonly object _gate = new();
        private readonly TimeProvider _clock;
        private readonly int _capacity, _queueLimit, _refill;
        private readonly TimeSpan _period;
        private readonly LinkedList<Waiter> _queue = new();
        private readonly ITimer? _timer;
        private long _lastRefill;
        private int _available;
        private bool _disposed;
        public object Options = null!;
        public ResiliencePipelineIdentity Identity = null!;
        public bool IsRate;
        public Limiter(TimeProvider clock, int capacity, int queueLimit, int refill, TimeSpan period)
        {
            _clock = clock; _available = _capacity = capacity; _queueLimit = queueLimit; _refill = refill; _period = period;
            _lastRefill = clock.GetTimestamp();
            if (refill > 0) _timer = clock.CreateTimer(_ => Tick(), null, period, period);
        }
        public ValueTask<IDisposable> Acquire(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Limiter));
                Refill();
                if (_available > 0 && _queue.Count == 0) { _available--; return new(new Lease(this)); }
                if (_queue.Count >= _queueLimit)
                    throw new ResilienceRejectedException(IsRate ? ResilienceRejectionReason.RateLimit : ResilienceRejectionReason.Concurrency, Identity);
                var waiter = new Waiter();
                waiter.Node = _queue.AddLast(waiter);
                var registration = token.Register(() =>
                {
                    lock (_gate)
                    {
                        if (waiter.Node.List is null) return;
                        _queue.Remove(waiter.Node);
                        waiter.Completion.TrySetCanceled(token);
                    }
                });
                return Await(waiter, registration);
            }
        }
        private static async ValueTask<IDisposable> Await(Waiter waiter, CancellationTokenRegistration registration)
        {
            using (registration) return await waiter.Completion.Task.ConfigureAwait(false);
        }
        private void Tick()
        {
            lock (_gate) { if (!_disposed) { Refill(); Drain(); } }
        }
        private void Refill()
        {
            if (_refill == 0) return;
            var now = _clock.GetTimestamp();
            var elapsed = _clock.GetElapsedTime(_lastRefill, now);
            var periods = elapsed.Ticks / _period.Ticks;
            if (periods <= 0) return;
            _available = (int)Math.Min(_capacity, _available + Math.Min(periods, _capacity) * (long)_refill);
            _lastRefill += (long)(periods * _period.TotalSeconds * _clock.TimestampFrequency);
        }
        private void Drain()
        {
            while (_available > 0 && _queue.First is { } node)
            {
                _queue.RemoveFirst(); _available--;
                node.Value.Completion.TrySetResult(new Lease(this));
            }
        }
        private void Release()
        {
            lock (_gate) { if (_disposed || _refill > 0) return; _available++; Drain(); }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _timer?.Dispose();
                foreach (var waiter in _queue) waiter.Completion.TrySetException(new ObjectDisposedException(nameof(Limiter)));
                _queue.Clear();
            }
        }
        private sealed class Waiter
        {
            public LinkedListNode<Waiter> Node = null!;
            public TaskCompletionSource<IDisposable> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private sealed class Lease(Limiter owner) : IDisposable
        {
            private Limiter? _owner = owner;
            public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}

/// <summary>统一使用注入时钟的异步等待。</summary>
public static class ResilienceTime
{
    public static Task DelayAsync(TimeProvider clock, TimeSpan delay, CancellationToken token = default)
    {
#if NET8_0_OR_GREATER
        return Task.Delay(delay, clock, token);
#else
        return clock.Delay(delay, token);
#endif
    }
}
