namespace Bing.Resilience;

/// <summary>稳定句柄注册表。一次调用租用同一代，状态资源属于宿主，原生引擎资源属于代。</summary>
internal sealed partial class ResilienceRegistry : IResiliencePipelineProvider, IResilienceCircuitController, IResilienceCircuitStateReader,
    IResilienceRuntimeManager, IResilienceRuntimeLeaseProvider, IResiliencePartitionedPipelineProvider, IDisposable, IAsyncDisposable
{
    private readonly Dictionary<ResiliencePipelineIdentity, IPipelineRegistration> _registrations;
    private readonly Dictionary<string, IResilienceProvider> _providers;
    private readonly Dictionary<ResiliencePipelineIdentity, object> _handles = new();
    private readonly Dictionary<string, Group> _groups = new(StringComparer.Ordinal);
    private readonly string? _defaultProvider;
    private readonly TimeProvider _clock;
    private readonly ResilienceRuntimeOptions? _runtime;
    private readonly ResilienceAdmissionResources _admission;
    private readonly ResilienceCircuitResources _sharedCircuits;
    private readonly object _gate = new();
    private bool _disposed, _validated, _admissionDisposed;
    private static readonly ResiliencePropertyKey<Scope?> ScopeKey = new("resilience.generation", scope => scope);
    private sealed record Scope(Generation Generation, Scope? Parent);
    private sealed record CircuitControl(Func<ResilienceCircuitState> State, Func<CancellationToken, ValueTask> Isolate, Func<CancellationToken, ValueTask> Close);
    private sealed class Group(string name, IPipelineRegistration[] registrations)
    {
        public string Name { get; } = name;
        public List<IPipelineRegistration> Registrations { get; } = registrations.ToList();
        public Generation? Active;
        public List<Generation> Retired { get; } = new();
    }
    private sealed class Generation(string group, long version, string? provider)
    {
        public string Group { get; } = group;
        public long Version { get; } = version;
        public string? Provider { get; } = provider;
        public Dictionary<ResiliencePipelineIdentity, object> Pipelines { get; } = new();
        public Dictionary<ResiliencePipelineIdentity, CircuitControl> Circuits { get; } = new();
        public Dictionary<ResiliencePipelineIdentity, HashSet<IDisposable>> Resources { get; } = new();
        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int References, Disposing;
        public bool Retired;
        public bool DisposeResources()
        {
            if (Interlocked.Exchange(ref Disposing, 1) != 0) return false;
            List<Exception>? errors = null;
            foreach (var resource in Resources.Values.SelectMany(x => x).Distinct(ReferenceEqualityComparer.Instance).Cast<IDisposable>().Reverse())
                try { resource.Dispose(); } catch (Exception e) { (errors ??= new()).Add(e); }
            Resources.Clear(); Pipelines.Clear(); Circuits.Clear();
            if (errors is null) Drained.TrySetResult();
            else Drained.TrySetException(new AggregateException("容错代资源释放失败。", errors));
            return true;
        }
    }
    public ResilienceRegistry(IEnumerable<IPipelineRegistration> registrations, IEnumerable<IResilienceProvider> providers,
        IEnumerable<DefaultProvider> defaults, TimeProvider clock, IEnumerable<ResilienceRuntimeOptions> runtime)
    {
        _registrations = registrations.ToDictionary(x => x.Identity);
        try { _providers = providers.ToDictionary(x => x.Name, StringComparer.Ordinal); }
        catch (ArgumentException) { throw new ResilienceConfigurationException("Provider 重复注册。"); }
        _defaultProvider = defaults.SingleOrDefault()?.Name;
        _clock = clock; _runtime = runtime.SingleOrDefault();
        _admission = new(clock); _sharedCircuits = new(clock);
        foreach (var group in _registrations.Values.GroupBy(x => x.Group, StringComparer.Ordinal))
            _groups.Add(group.Key, new(group.Key, group.ToArray()));
    }
    public bool IsRuntimeSwitchingEnabled => _runtime is not null;
    public void Validate()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_validated) return;
            if (_defaultProvider is null || !_providers.ContainsKey(_defaultProvider))
                throw new ResilienceConfigurationException("必须显式指定已注册的默认 Provider。");
            foreach (var registration in _registrations.Values) ValidateProvider(registration, ResolveProvider(registration.ProviderName));
            _validated = true;
        }
    }
    private void ValidateProvider(IPipelineRegistration registration, IResilienceProvider provider)
    {
        if ((provider.Capabilities & registration.Capabilities) != registration.Capabilities)
            throw new ResilienceConfigurationException($"Provider '{provider.Name}' 不支持管线 '{registration.Identity.Name}' 所需能力。");
        if (_runtime is not null && provider is not IResilienceRuntimeAwareProvider)
            throw new ResilienceConfigurationException($"Provider '{provider.Name}' 未声明运行时资源协议。");
    }
    private IResilienceProvider ResolveProvider(string? name)
    {
        name ??= _defaultProvider;
        if (name is null || !_providers.TryGetValue(name, out var provider))
            throw new ResilienceConfigurationException($"未知 Provider '{name}'。");
        return provider;
    }
    public IResiliencePipeline<T> GetPipeline<T>(string name, string purpose = "generic", string partition = "")
    {
        Validate();
        var identity = new ResiliencePipelineIdentity(purpose, name, typeof(T), partition);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_registrations.TryGetValue(identity, out var registration)) throw new ResilienceConfigurationException($"未注册管线 '{identity}'。");
            if (!_handles.TryGetValue(identity, out var handle)) _handles.Add(identity, handle = registration.CreateHandle(this));
            return (IResiliencePipeline<T>)handle;
        }
    }
    public IResiliencePipeline GetPipeline(string name, string purpose = "generic", string partition = "") =>
        new VoidPipeline(GetPipeline<ResilienceUnit>(name, purpose, partition));

    private Group FindGroup(string group)
    {
        if (!_groups.TryGetValue(group, out var value)) throw new ResilienceConfigurationException($"未登记切换组 '{group}'。");
        return value;
    }
    private Generation Build(Group group, long version, string? overrideProvider)
    {
        var generation = new Generation(group.Name, version, overrideProvider);
        try
        {
            IPipelineRegistration[] registrations;
            lock (_gate) registrations = group.Registrations.ToArray();
            foreach (var registration in registrations) BuildComponent(generation, registration, overrideProvider);
            return generation;
        }
        catch { generation.DisposeResources(); throw; }
    }
    private void BuildComponent(Generation generation, IPipelineRegistration registration, string? overrideProvider)
    {
        var provider = ResolveProvider(overrideProvider ?? registration.ProviderName);
        ValidateProvider(registration, provider);
        var resources = new HashSet<IDisposable>(ReferenceEqualityComparer.Instance);
        generation.Resources.Add(registration.Identity, resources);
        var context = new ResiliencePipelineBuildContext(registration.Identity, _clock,
            resource => resources.Add(resource),
            (state, isolate, close) => generation.Circuits.Add(registration.Identity, new(state, isolate, close)),
            eventName => ResilienceDiagnostics.Event(registration.Identity, provider.Name, eventName))
            { AdmissionResources = _admission, CircuitResources = _sharedCircuits };
        generation.Pipelines.Add(registration.Identity, registration.Create(provider, context));
    }
    public IResilienceExecutionLease AcquireLease(string group, ResilienceExecutionContext? context = null)
    {
        Validate();
        lock (_gate)
        {
            ThrowIfDisposed();
            var value = FindGroup(group);
            context ??= new();
            context.TryGet(ScopeKey, out var parent);
            Generation? generation = null;
            for (var scope = parent; scope is not null; scope = scope.Parent)
                if (scope.Generation.Group == group) { generation = scope.Generation; break; }
            generation ??= value.Active ??= Build(value, 0, null);
            if (generation.Disposing != 0) throw new ObjectDisposedException("容错执行租约");
            generation.References++;
            var child = context.Fork(context.CancellationToken);
            child.Set(ScopeKey, new Scope(generation, parent));
            return new Lease(this, generation, child);
        }
    }
    internal IResiliencePipeline<T> Resolve<T>(IResilienceExecutionLease lease, ResiliencePipelineIdentity identity)
    {
        lock (_gate)
        {
            var generation = ((Lease)lease).Generation;
            if (!generation.Pipelines.TryGetValue(identity, out var pipeline))
            {
                if (!_registrations.TryGetValue(identity, out var registration)) throw new ResilienceConfigurationException("分区已退休。");
                BuildComponent(generation, registration, generation.Provider);
                pipeline = generation.Pipelines[identity];
            }
            return (IResiliencePipeline<T>)pipeline;
        }
    }
    private void Release(Generation generation)
    {
        bool dispose;
        lock (_gate) { generation.References--; dispose = generation.Retired && generation.References == 0; }
        if (dispose) DisposeGeneration(generation);
    }
    private void DisposeGeneration(Generation generation)
    {
        lock (_gate)
        {
            if (!generation.DisposeResources()) return;
            if (_groups.TryGetValue(generation.Group, out var group)) group.Retired.Remove(generation);
            DisposeAdmissionIfDrained();
        }
        ResilienceDiagnostics.RecordLifecycle("generation.drain", generation.Drained.Task.IsCompletedSuccessfully ? "success" : "failure");
    }
    private void DisposeAdmissionIfDrained()
    {
        if (_disposed && !_admissionDisposed && _groups.Values.All(x => x.Retired.Count == 0))
        { _admissionDisposed = true; _admission.Dispose(); }
    }
    public ResilienceRuntimeSnapshot GetSnapshot(string group)
    {
        Validate();
        lock (_gate)
        {
            var value = FindGroup(group);
            ThrowIfDisposed();
            var active = value.Active ??= Build(value, 0, null);
            return new(group, active.Version, active.Provider, active.References, value.Retired.Count);
        }
    }
    public async ValueTask<ResilienceSwitchResult> SwitchAsync(string group, string providerName, long expectedVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await SwitchCoreAsync(group, providerName, expectedVersion, cancellationToken).ConfigureAwait(false);
            ResilienceDiagnostics.RecordLifecycle("generation.switch", "success");
            if (!result.PreviousGenerationDrained) ResilienceDiagnostics.RecordLifecycle("generation.drain", "pending");
            return result;
        }
        catch (OperationCanceledException) { ResilienceDiagnostics.RecordLifecycle("generation.switch", "cancelled"); throw; }
        catch { ResilienceDiagnostics.RecordLifecycle("generation.switch", "failure"); throw; }
    }
    private async ValueTask<ResilienceSwitchResult> SwitchCoreAsync(string group, string providerName, long expectedVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Validate();
        if (_runtime is null) throw new ResilienceConfigurationException("尚未启用运行时切换。");
        Group value;
        Generation previous;
        lock (_gate)
        {
            ThrowIfDisposed(); value = FindGroup(group);
            previous = value.Active ??= Build(value, 0, null);
            if (previous.Version != expectedVersion) throw new ResilienceConfigurationException("切换版本冲突。");
            if (value.Retired.Count + 1 >= _runtime.MaxRetainedGenerations) throw new ResilienceConfigurationException("旧代尚未排空，已达到保留上限。");
        }
        // 候选完整构建后才进行 CAS 发布；构建失败不影响现役代。
        var candidate = Build(value, checked(expectedVersion + 1), providerName);
        bool disposePrevious;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (!ReferenceEquals(value.Active, previous)) throw new ResilienceConfigurationException("切换版本冲突。");
                foreach (var id in candidate.Pipelines.Keys.Where(id => !_registrations.ContainsKey(id)).ToArray()) RemoveComponent(candidate, id);
                previous.Retired = true; value.Retired.Add(previous); value.Active = candidate;
                disposePrevious = previous.References == 0;
            }
        }
        catch { candidate.DisposeResources(); throw; }
        if (disposePrevious) DisposeGeneration(previous);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = ResilienceTime.DelayAsync(_clock, _runtime.DrainTimeout, wait.Token);
        var completed = await Task.WhenAny(previous.Drained.Task, delay).ConfigureAwait(false);
        wait.Cancel();
        if (completed == previous.Drained.Task) await previous.Drained.Task.ConfigureAwait(false);
        // 发布之后调用者取消只结束排空等待，不撤销已完成的切换。
        return new(GetSnapshot(group), previous.Drained.Task.IsCompletedSuccessfully);
    }
    private (Lease Lease, CircuitControl Control) Control(ResiliencePipelineIdentity identity)
    {
        if (!_registrations.TryGetValue(identity, out var registration) ||
            !registration.Capabilities.HasFlag(ResilienceCapabilities.ManualCircuitControl))
            throw new ResilienceConfigurationException($"管线 '{identity}' 未启用手动熔断控制。");
        var lease = (Lease)AcquireLease(registration.Group);
        lock (_gate)
        {
            if (!lease.Generation.Circuits.TryGetValue(identity, out var control))
            {
                // 动态分区可能在当前代创建之后才登记；手动控制也必须在首次执行前建立组件。
                if (lease.Generation.Pipelines.ContainsKey(identity))
                    throw new ResilienceConfigurationException($"管线 '{identity}' 未创建可控熔断器。");
                BuildComponent(lease.Generation, registration, lease.Generation.Provider);
                if (!lease.Generation.Circuits.TryGetValue(identity, out control))
                    throw new ResilienceConfigurationException($"管线 '{identity}' 未创建可控熔断器。");
            }
            return (lease, control);
        }
    }
    public ResilienceCircuitSnapshot GetCircuitSnapshot(ResiliencePipelineIdentity identity)
    {
        var (lease, _) = Control(identity); using (lease) return _sharedCircuits.GetSnapshot(identity);
    }
    public ResilienceCircuitState GetState(ResiliencePipelineIdentity identity)
    {
        var (lease, control) = Control(identity); using (lease) return control.State();
    }
    public async ValueTask IsolateAsync(ResiliencePipelineIdentity identity, CancellationToken cancellationToken = default)
    {
        var (lease, control) = Control(identity); using (lease) await control.Isolate(cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask CloseAsync(ResiliencePipelineIdentity identity, CancellationToken cancellationToken = default)
    {
        var (lease, control) = Control(identity); using (lease) await control.Close(cancellationToken).ConfigureAwait(false);
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ResilienceRegistry)); }
    public void Dispose()
    {
        List<Generation> dispose = new();
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _handles.Clear(); _partitionTimer?.Dispose();
            foreach (var group in _groups.Values)
            {
                if (group.Active is { } active) { active.Retired = true; group.Retired.Add(active); group.Active = null; }
                dispose.AddRange(group.Retired.Where(x => x.References == 0));
            }
            DisposeAdmissionIfDrained();
        }
        foreach (var generation in dispose) DisposeGeneration(generation);
    }
    public async ValueTask DisposeAsync()
    {
        Dispose();
        Task[] tasks;
        lock (_gate) tasks = _groups.Values.SelectMany(x => x.Retired).Select(x => x.Drained.Task).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
    private sealed class Lease(ResilienceRegistry owner, Generation generation, ResilienceExecutionContext context) : IResilienceExecutionLease
    {
        private ResilienceRegistry? _owner = owner;
        public Generation Generation { get; } = generation;
        public long Version => Generation.Version;
        public ResilienceExecutionContext Context { get; } = context;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(Generation);
    }
    private sealed class VoidPipeline(IResiliencePipeline<ResilienceUnit> pipeline) : IResiliencePipeline
    {
        public async ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            await pipeline.ExecuteAsync(async ct => { await action(ct).ConfigureAwait(false); return default; }, cancellationToken).ConfigureAwait(false);
        }
        public async ValueTask ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask> action, ResilienceExecutionContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            await pipeline.ExecuteAsync(async (ctx, ct) => { await action(ctx, ct).ConfigureAwait(false); return default; }, context, cancellationToken).ConfigureAwait(false);
        }
    }
}
/// <summary>缓存此句柄不会缓存具体引擎代；嵌套上下文保持同代。</summary>
internal sealed class StablePipeline<T>(ResilienceRegistry registry, ResiliencePipelineIdentity identity, string group) : IResiliencePipeline<T>
{
    public ValueTask<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync((_, ct) => action(ct), new(), cancellationToken);
    }
    public async ValueTask<T> ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> action,
        ResilienceExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action); ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = registry.AcquireLease(group, context);
        return await registry.Resolve<T>(lease, identity).ExecuteAsync(action, lease.Context, cancellationToken).ConfigureAwait(false);
    }
}
