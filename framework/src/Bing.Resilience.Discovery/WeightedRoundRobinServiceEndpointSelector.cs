namespace Bing.Resilience.Discovery;

/// <summary>平滑加权轮转：每次调用选定一个主实例，其余实例用于本次调用的故障转移。</summary>
/// <remarks>状态按服务隔离，成员或权重变化时重置；空闲状态与超出容量的最久未使用状态会被回收。</remarks>
public sealed class WeightedRoundRobinServiceEndpointSelector : IServiceEndpointSelector
{
    private readonly object _gate = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private readonly int _maximumServices;
    private readonly int _maximumEndpoints;
    private readonly TimeSpan _retention;
    private readonly TimeProvider _clock;

    public WeightedRoundRobinServiceEndpointSelector(int maximumServices = 128, int maximumEndpoints = 128,
        TimeSpan? stateRetention = null, TimeProvider? timeProvider = null)
    {
        _retention = stateRetention ?? TimeSpan.FromMinutes(5);
        if (maximumServices < 1 || maximumEndpoints < 1 || _retention < TimeSpan.Zero)
            throw new ResilienceConfigurationException("轮转状态容量必须为正，保留时间不能为负。");
        _maximumServices = maximumServices; _maximumEndpoints = maximumEndpoints;
        _clock = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<ServiceEndpoint> Select(ServiceEndpointSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Endpoints.Count > _maximumEndpoints || snapshot.Endpoints.Any(e => e.Weight <= 0) ||
            snapshot.Endpoints.Select(e => e.StateKey).Distinct(StringComparer.Ordinal).Count() != snapshot.Endpoints.Count)
            throw new ResilienceConfigurationException("轮转候选超出容量、权重非正或身份重复。");
        var endpoints = snapshot.Endpoints.OrderBy(e => e.StateKey, StringComparer.Ordinal).ToArray();
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            foreach (var key in _states.Where(x => now - x.Value.LastUsed >= _retention).Select(x => x.Key).ToArray())
                _states.Remove(key);
            if (endpoints.Length == 0) { _states.Remove(snapshot.ServiceName); return Array.Empty<ServiceEndpoint>(); }
            if (!_states.TryGetValue(snapshot.ServiceName, out var state) || !state.Endpoints.SequenceEqual(endpoints))
            {
                if (!_states.ContainsKey(snapshot.ServiceName) && _states.Count >= _maximumServices)
                    _states.Remove(_states.OrderBy(x => x.Value.LastUsed).First().Key);
                _states[snapshot.ServiceName] = state = new(endpoints);
            }
            state.LastUsed = now;
            long total = 0;
            var selected = 0;
            for (var i = 0; i < endpoints.Length; i++)
            {
                total += endpoints[i].Weight;
                state.Current[i] += endpoints[i].Weight;
                if (state.Current[i] > state.Current[selected]) selected = i;
            }
            state.Current[selected] -= total;
            // 返回独立只读快照；后续调用推进轮转不会改变已开始调用的候选顺序。
            return Array.AsReadOnly(new[] { endpoints[selected] }.Concat(endpoints.Where((_, i) => i != selected)).ToArray());
        }
    }

    private sealed class State(ServiceEndpoint[] endpoints)
    {
        public ServiceEndpoint[] Endpoints { get; } = endpoints;
        public long[] Current { get; } = new long[endpoints.Length];
        public DateTimeOffset LastUsed { get; set; }
    }
}
