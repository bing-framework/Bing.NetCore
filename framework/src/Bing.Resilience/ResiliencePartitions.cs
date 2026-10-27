namespace Bing.Resilience;

internal sealed partial class ResilienceRegistry
{
    private sealed class PartitionEntry(IPipelineRegistration registration, TimeSpan retention, long now)
    {
        public IPipelineRegistration Registration { get; } = registration;
        public TimeSpan Retention { get; } = retention;
        public int References;
        public long LastUsed = now;
    }
    private readonly Dictionary<ResiliencePipelineIdentity, PartitionEntry> _partitions = new();
    private ITimer? _partitionTimer;
    public int PartitionCount { get { lock (_gate) return _partitions.Count; } }
    public IResiliencePartitionLease<T> AcquirePartition<T>(string name, string purpose, string partition,
        int maximumPartitions = 128, TimeSpan? retention = null)
    {
        Validate();
        if (string.IsNullOrWhiteSpace(partition) || maximumPartitions < 1 || maximumPartitions > 10000)
            throw new ResilienceConfigurationException("动态分区身份或数量上限无效。");
        lock (_gate)
        {
            ThrowIfDisposed(); CollectPartitions();
            var templateId = new ResiliencePipelineIdentity(purpose, name, typeof(T));
            if (!_registrations.TryGetValue(templateId, out var template)) throw new ResilienceConfigurationException("缺少动态分区模板。");
            var keep = retention ?? TimeSpan.FromMinutes(5);
            if (keep < template.MinimumRetention || keep < TimeSpan.Zero || keep > TimeSpan.FromDays(1))
                throw new ResilienceConfigurationException("分区保留时间不能短于熔断采样窗口和打开时间。");
            var id = templateId with { Partition = partition };
            if (!_partitions.TryGetValue(id, out var entry))
            {
                if (_registrations.ContainsKey(id)) throw new ResilienceConfigurationException("动态分区与静态登记冲突。");
                if (_partitions.Keys.Count(x => x.Name == name && x.Purpose == purpose && x.ResultType == typeof(T)) >= maximumPartitions)
                    throw new ResilienceConfigurationException("动态分区已达到上限，等待旧快照排空与冷却。");
                var registration = template.WithPartition(partition);
                entry = new(registration, keep, _clock.GetTimestamp());
                _partitions.Add(id, entry); _registrations.Add(id, registration);
                FindGroup(registration.Group).Registrations.Add(registration);
                _partitionTimer ??= _clock.CreateTimer(_ => CollectPartitionsInBackground(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
            else if (keep != entry.Retention) throw new ResilienceConfigurationException("同一动态分区不能混用保留策略。");
            entry.References++;
            return new PartitionLease<T>(this, id, GetPipeline<T>(name, purpose, partition));
        }
    }
    private void CollectPartitionsInBackground()
    {
        try { CollectPartitions(); }
        catch { ResilienceDiagnostics.Event(new ResiliencePipelineIdentity("system", "partition.cleanup", typeof(void)), "core", "partition.cleanup_failed"); }
    }
    public int CollectPartitions()
    {
        lock (_gate)
        {
            if (_disposed) return 0;
            var retired = _partitions.Where(x => x.Value.References == 0 &&
                _clock.GetElapsedTime(x.Value.LastUsed) >= x.Value.Retention).ToArray();
            foreach (var item in retired)
            {
                var id = item.Key;
                var group = FindGroup(item.Value.Registration.Group);
                foreach (var generation in group.Retired.Concat(group.Active is { } active ? new[] { active } : Array.Empty<Generation>()))
                    RemoveComponent(generation, id);
                group.Registrations.Remove(item.Value.Registration);
                _registrations.Remove(id); _handles.Remove(id); _partitions.Remove(id);
                _admission.Retire(id); _sharedCircuits.Retire(id);
            }
            return retired.Length;
        }
    }
    private static void RemoveComponent(Generation generation, ResiliencePipelineIdentity identity)
    {
        generation.Pipelines.Remove(identity); generation.Circuits.Remove(identity);
        if (!generation.Resources.Remove(identity, out var resources)) return;
        foreach (var resource in resources.Reverse()) resource.Dispose();
    }
    private void ReleasePartition(ResiliencePipelineIdentity identity)
    {
        lock (_gate)
        {
            if (_partitions.TryGetValue(identity, out var entry)) { entry.References--; entry.LastUsed = _clock.GetTimestamp(); }
        }
    }
    private sealed class PartitionLease<T>(ResilienceRegistry registry, ResiliencePipelineIdentity identity, IResiliencePipeline<T> pipeline) : IResiliencePartitionLease<T>
    {
        private ResilienceRegistry? _registry = registry;
        public IResiliencePipeline<T> Pipeline => _registry is null ? throw new ObjectDisposedException(nameof(PartitionLease<T>)) : pipeline;
        public void Dispose() => Interlocked.Exchange(ref _registry, null)?.ReleasePartition(identity);
    }
}
