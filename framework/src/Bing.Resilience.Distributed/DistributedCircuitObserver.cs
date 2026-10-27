using System.Security.Cryptography;
using System.Text;

namespace Bing.Resilience.Distributed;

/// <summary>在每次真实尝试前执行跨节点准入；报告失败不会把已提交业务重新变成失败。</summary>
public sealed class DistributedCircuitObserver<T> : IResilienceAttemptObserver<T>
{
    private readonly IDistributedCircuitStateStore _store;
    private readonly DistributedCircuitOptions _options;
    private readonly Func<ResilienceOutcome<T>, bool> _failure;
    private readonly TimeProvider _clock;
    private readonly ResilienceCircuitResources? _local;
    private readonly Dictionary<string, LocalCircuit> _locals = new(StringComparer.Ordinal);
    private sealed class LocalCircuit(IResilienceSharedCircuit circuit, ResiliencePipelineIdentity identity)
    { public IResilienceSharedCircuit Circuit { get; } = circuit; public ResiliencePipelineIdentity Identity { get; } = identity; public int References; public DateTimeOffset LastUsed; }
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (DistributedCircuitAdmission Admission, DateTimeOffset Until)> _cachedOpen = new(StringComparer.Ordinal);
    public DistributedCircuitObserver(IDistributedCircuitStateStore store, DistributedCircuitOptions options,
        Func<ResilienceOutcome<T>, bool> shouldHandle, TimeProvider? clock = null)
    {
        options.Configuration.Validate();
        if (string.IsNullOrWhiteSpace(options.CircuitKey) || options.FailureMode is null ||
            !Enum.IsDefined(typeof(DistributedCircuitFailureMode), options.FailureMode.Value) ||
            !Enum.IsDefined(typeof(DistributedCircuitScope), options.Scope) || !Enum.IsDefined(typeof(DistributedCircuitReadMode), options.ReadMode) ||
            options.MaximumLocalCircuits < 1 || options.MaximumLocalCircuits > 10000 || options.CircuitKey.Length > 384 ||
            options.StoreTimeout <= TimeSpan.Zero || options.StoreTimeout > TimeSpan.FromSeconds(30) ||
            (options.ReadMode == DistributedCircuitReadMode.CachedRead && (options.CacheDuration <= TimeSpan.Zero || options.CacheDuration > TimeSpan.FromSeconds(30))))
            throw new ResilienceConfigurationException("分布式熔断必须声明稳定键、失联策略及有界存储预算/缓存滞后。");
        _store = store; _failure = shouldHandle; _clock = clock ?? TimeProvider.System;
        _options = new()
        {
            CircuitKey = options.CircuitKey, Configuration = options.Configuration, FailureMode = options.FailureMode, Scope = options.Scope,
            ReadMode = options.ReadMode, CacheDuration = options.CacheDuration, StoreTimeout = options.StoreTimeout, OnStoreFailure = options.OnStoreFailure,
            MaximumLocalCircuits = options.MaximumLocalCircuits
        };
        if (options.FailureMode == DistributedCircuitFailureMode.FailOpenWithLocalCircuit) _local = new(_clock);
    }
    /// <summary>管理操作应使用与尝试相同的键；Endpoint 模式包含实际管线的稳定分区身份。</summary>
    public string GetCircuitKey(ResilienceAttemptContext context) => _options.Scope == DistributedCircuitScope.Service
        ? _options.CircuitKey
        : _options.CircuitKey + ":endpoint:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            context.Identity.Name.Length + ":" + context.Identity.Name + context.Identity.Partition.Length + ":" + context.Identity.Partition)));
    private IResilienceCircuitLease EnterLocal(string key)
    {
        lock (_cacheGate)
        {
            var now = _clock.GetUtcNow();
            var c = _options.Configuration;
            var retention = TimeSpan.FromMilliseconds(Math.Max(c.SamplingMilliseconds, c.BreakMilliseconds));
            foreach (var old in _locals.Where(x => x.Value.References == 0 && now - x.Value.LastUsed > retention &&
                         x.Value.Circuit.State == ResilienceCircuitState.Closed).ToArray())
            { _local!.Retire(old.Value.Identity); _locals.Remove(old.Key); }
            if (!_locals.TryGetValue(key, out var entry))
            {
                if (_locals.Count >= _options.MaximumLocalCircuits)
                    throw new ResilienceStateStoreUnavailableException(new InvalidOperationException("本地熔断分区达到上限。"));
                var identity = new ResiliencePipelineIdentity("distributed.local", key, typeof(T));
                entry = new(_local!.GetCircuit(identity, new(c.FailureRatio, c.MinimumThroughput,
                    TimeSpan.FromMilliseconds(c.SamplingMilliseconds), TimeSpan.FromMilliseconds(c.BreakMilliseconds)), _ => { }), identity);
                _locals.Add(key, entry);
            }
            entry.LastUsed = now;
            var lease = entry.Circuit.Enter();
            entry.References++;
            return new LocalLease(lease, () => { lock (_cacheGate) { entry.References--; entry.LastUsed = _clock.GetUtcNow(); } });
        }
    }
    private sealed class LocalLease(IResilienceCircuitLease inner, Action release) : IResilienceCircuitLease
    {
        private int _disposed;
        public void Complete(bool failure, bool excluded = false) => inner.Complete(failure, excluded);
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) { try { inner.Dispose(); } finally { release(); } } }
    }
    public async ValueTask<IResilienceAttemptObservation<T>> BeginAsync(ResilienceAttemptContext context, CancellationToken cancellationToken)
    {
        try { return await BeginCoreAsync(context, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { ResilienceDiagnostics.RecordLifecycle("distributed.admission", "cancelled"); throw; }
        catch (DistributedCircuitRejectedException) { ResilienceDiagnostics.RecordLifecycle("distributed.admission", "rejected"); throw; }
        catch (ResilienceCircuitOpenException) { ResilienceDiagnostics.RecordLifecycle("distributed.admission", "local_rejected"); throw; }
        catch { ResilienceDiagnostics.RecordLifecycle("distributed.admission", "failure"); throw; }
    }
    private async ValueTask<IResilienceAttemptObservation<T>> BeginCoreAsync(ResilienceAttemptContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = GetCircuitKey(context);
        if (_options.ReadMode == DistributedCircuitReadMode.CachedRead)
            lock (_cacheGate)
                if (_cachedOpen.TryGetValue(key, out var denied) && denied.Until > _clock.GetUtcNow())
                    throw new DistributedCircuitRejectedException(denied.Admission.CircuitKey, _options.Scope, denied.Until - _clock.GetUtcNow());
        DistributedCircuitAdmission admission;
        try
        {
            admission = await WithBudget(token => _store.AdmitAsync(key, _options.Configuration, Guid.NewGuid().ToString("N"), token), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not DistributedCircuitVersionException && !cancellationToken.IsCancellationRequested)
        {
            ReportStoreFailure(error);
            if (_local is null) throw new ResilienceStateStoreUnavailableException(error);
            var observation = new Observation(this, null, EnterLocal(key));
            ResilienceDiagnostics.RecordLifecycle("distributed.admission", "local_allowed");
            return observation;
        }
        if (!admission.Allowed)
        {
            if (_options.ReadMode == DistributedCircuitReadMode.CachedRead)
                lock (_cacheGate)
                {
                    foreach (var old in _cachedOpen.Where(x => x.Value.Until <= _clock.GetUtcNow()).ToArray()) _cachedOpen.Remove(old.Key);
                    if (_cachedOpen.Count < _options.MaximumLocalCircuits || _cachedOpen.ContainsKey(key))
                        _cachedOpen[key] = (admission, _clock.GetUtcNow() + (_options.CacheDuration < admission.RetryAfter ? _options.CacheDuration : admission.RetryAfter));
                }
            throw new DistributedCircuitRejectedException(admission.CircuitKey, _options.Scope, admission.RetryAfter);
        }
        ResilienceDiagnostics.RecordLifecycle("distributed.admission", "allowed");
        return new Observation(this, admission, null);
    }
    private async ValueTask<TResult> WithBudget<TResult>(Func<CancellationToken, ValueTask<TResult>> callback, CancellationToken parent)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(parent);
        using var timer = _clock.CreateTimer(_ => { try { budget.Cancel(); } catch (ObjectDisposedException) { } }, null, _options.StoreTimeout, Timeout.InfiniteTimeSpan);
        return await callback(budget.Token).ConfigureAwait(false);
    }
    private void ReportStoreFailure(Exception error)
    {
        try { _options.OnStoreFailure?.Invoke(error); } catch { /* 诊断回调不得改变业务结果。 */ }
    }
    private sealed class Observation(DistributedCircuitObserver<T> owner, DistributedCircuitAdmission? admission, IResilienceCircuitLease? local) : IResilienceAttemptObservation<T>
    {
        private int _completed;
        public async ValueTask CompleteAsync(ResilienceOutcome<T> outcome, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0) return;
            var excluded = cancellationToken.IsCancellationRequested || outcome.Exception is OperationCanceledException or
                ResilienceRejectedException or ResilienceCircuitOpenException or DistributedCircuitRejectedException or IResilienceNonRecoverableFailure;
            var result = excluded ? DistributedCircuitOutcome.Excluded : owner._failure(outcome) ? DistributedCircuitOutcome.Failure : DistributedCircuitOutcome.Success;
            local?.Complete(result == DistributedCircuitOutcome.Failure, excluded);
            if (admission is null) return;
            try
            {
                // 独立短上报预算；业务令牌取消不代表 Redis 脚本没有执行。
                await owner.WithBudget(async token =>
                {
                    await owner._store.ReportAsync(admission, owner._options.Configuration, result, token).ConfigureAwait(false); return true;
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) { owner.ReportStoreFailure(error); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref _completed) == 0)
                await CompleteAsync(ResilienceOutcome<T>.FromException(new OperationCanceledException()), CancellationToken.None).ConfigureAwait(false);
            local?.Dispose();
        }
    }
}
