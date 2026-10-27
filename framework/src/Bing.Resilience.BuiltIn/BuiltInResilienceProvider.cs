using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Bing.Resilience;

/// <summary>无第三方容错引擎依赖的 Provider 注册入口。</summary>
public static class BuiltInResilienceExtensions
{
    public static BingResilienceBuilder AddBuiltInProvider(this BingResilienceBuilder builder, string name = "builtin") =>
        builder.AddProvider(new BuiltInResilienceProvider(name));
}

/// <summary>完整实现中性策略契约的内置引擎。</summary>
public sealed class BuiltInResilienceProvider(string name) : IResilienceRuntimeAwareProvider
{
    public string Name { get; } = name;
    public ResilienceCapabilities Capabilities => ResilienceCapabilities.All;
    public IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context) =>
        new BuiltInPipeline<T>(definition, context);
}

internal sealed class BuiltInPipeline<T> : IResiliencePipeline<T>
{
    private readonly ResiliencePipelineDefinition<T> _d;
    private readonly ResiliencePipelineBuildContext _build;
    private readonly IResilienceAdmissionResources _admission;
    private readonly IResilienceSharedCircuit? _circuit;
    private sealed class Result(T value)
    {
        public T Value { get; } = value;
        public int Discarded;
    }
    private sealed record Outcome(Result? Result, Exception? Error)
    {
        public ResilienceOutcome<T> Neutral => Error is null ? ResilienceOutcome<T>.FromResult(Result!.Value) : ResilienceOutcome<T>.FromException(Error);
    }
    private sealed class Execution(Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> callback, ResilienceExecutionContext context)
    {
        public Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> Callback { get; } = callback;
        public ResilienceExecutionContext Context { get; } = context;
        public ConcurrentBag<Result> Results { get; } = new();
        public Result Record(T value) { var result = new Result(value); Results.Add(result); return result; }
    }
    public BuiltInPipeline(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext build)
    {
        _d = definition; _build = build;
        _admission = build.AdmissionResources ?? CreateAdmission();
        if (_d.CircuitBreaker.Enabled)
        {
            var c = _d.CircuitBreaker;
            _circuit = (build.CircuitResources ?? new ResilienceCircuitResources(build.TimeProvider)).GetCircuit(_d.Identity,
                new(c.FailureRatio, c.MinimumThroughput, c.SamplingDuration, c.BreakDuration), build.ReportEvent);
            if (_d.CircuitBreaker.EnableManualControl) build.RegisterCircuit(() => _circuit.State, _circuit.IsolateAsync, _circuit.CloseAsync);
        }
    }
    private IResilienceAdmissionResources CreateAdmission()
    {
        var resource = new ResilienceAdmissionResources(_build.TimeProvider); _build.RegisterResource(resource); return resource;
    }

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
        using var budget = new Budget(_build.TimeProvider, _d.ExecutionTimeout, cancellationToken);
        var token = budget.Token;
        var execution = new Execution(action, context.Fork(token, nestedExecution: true));
        using var permit = _d.Concurrency.Enabled
            ? await AcquireConcurrency(budget, cancellationToken).ConfigureAwait(false) : null;
        Result? selected = null;
        try
        {
            var outcome = _d.Hedging.Enabled
                ? await Hedge(execution, token).ConfigureAwait(false)
                : await Retry(execution, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (_d.Fallback.Enabled && Handle(_d.Fallback.ShouldHandle!, outcome, execution, 0, token, false))
            {
                if (outcome.Result is { } old) await Discard(old).ConfigureAwait(false);
                _build.ReportEvent("fallback");
                var value = await _d.Fallback.Action!(new(outcome.Neutral, execution.Context.Fork(token), token)).ConfigureAwait(false);
                outcome = new(execution.Record(value), null);
            }
            token.ThrowIfCancellationRequested();
            if (outcome.Error is not null) ExceptionDispatchInfo.Capture(outcome.Error).Throw();
            selected = outcome.Result!;
        }
        catch (Exception e) when (budget.Expired && !cancellationToken.IsCancellationRequested && e is not ResilienceResultCleanupException)
        {
            _build.ReportEvent("timeout.execution");
            throw new ResilienceTimeoutException(ResilienceTimeoutKind.Execution, _d.Identity, e);
        }
        finally
        {
            List<Exception>? errors = null;
            // 清理完成之前持有逻辑并发许可。未获胜、超时和迟到结果均只释放一次。
            foreach (var result in execution.Results)
                if (!ReferenceEquals(result, selected))
                    try { await Discard(result).ConfigureAwait(false); }
                    catch (Exception e) { (errors ??= new()).Add(e); }
            if (errors is not null || token.IsCancellationRequested)
            {
                if (selected is not null)
                    try { await Discard(selected).ConfigureAwait(false); }
                    catch (Exception e) { (errors ??= new()).Add(e); }
                cancellationToken.ThrowIfCancellationRequested();
                if (errors is not null) throw new ResilienceResultCleanupException(_d.Identity, new AggregateException(errors));
                if (budget.Expired) throw new ResilienceTimeoutException(ResilienceTimeoutKind.Execution, _d.Identity);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return selected!.Value;
    }

    private async ValueTask<IDisposable> AcquireConcurrency(Budget budget, CancellationToken caller)
    {
        try { return await _admission.AcquireConcurrencyAsync(_d.Identity, _d.Concurrency, budget.Token).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (budget.Expired && !caller.IsCancellationRequested)
        {
            _build.ReportEvent("timeout.execution");
            throw new ResilienceTimeoutException(ResilienceTimeoutKind.Execution, _d.Identity, error);
        }
    }

    private async ValueTask<Outcome> Retry(Execution execution, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var outcome = await Attempt(execution, attempt, token).ConfigureAwait(false);
            if (attempt >= _d.Retry.MaxRetryAttempts || !Handle(_d.Retry.ShouldHandle!, outcome, execution, attempt, token, true)) return outcome;
            var args = new ResiliencePredicateArguments<T>(outcome.Neutral, execution.Context.Fork(token, attempt), attempt);
            var delay = _d.Retry.DelayGenerator?.Invoke(args) ?? TimeSpan.FromTicks((long)Math.Min(_d.Retry.MaxDelay.Ticks,
                _d.Retry.Delay.Ticks * Math.Pow(2, attempt) * (_d.Retry.UseJitter ? .5 + Random.Shared.NextDouble() : 1)));
            if (delay < TimeSpan.Zero) throw new ResilienceConfigurationException("退避生成器不能返回负数。");
            if (outcome.Result is { } result) await Discard(result).ConfigureAwait(false);
            _build.ReportEvent("retry");
            await ResilienceTime.DelayAsync(_build.TimeProvider, delay, token).ConfigureAwait(false);
        }
    }

    private async ValueTask<Outcome> Hedge(Execution execution, CancellationToken token)
    {
        using var branches = CancellationTokenSource.CreateLinkedTokenSource(token);
        var tasks = new List<Task<Outcome>> { Attempt(execution, 0, branches.Token).AsTask() };
        var pending = new List<Task<Outcome>>(tasks);
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task delay = ResilienceTime.DelayAsync(_build.TimeProvider, _d.Hedging.Delay, delayCancellation.Token);
        Outcome? selected = null;
        try
        {
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var mayLaunch = tasks.Count <= _d.Hedging.MaxHedgedAttempts;
                var finished = mayLaunch ? await Task.WhenAny(pending.Cast<Task>().Append(delay)).ConfigureAwait(false)
                    : await Task.WhenAny(pending).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (finished != delay)
                {
                    var completed = (Task<Outcome>)finished;
                    pending.Remove(completed);
                    var outcome = await completed.ConfigureAwait(false);
                    if (!Handle(_d.Hedging.ShouldHandle!, outcome, execution, tasks.IndexOf(completed), token, false))
                    { selected = outcome; break; }
                }
                if (mayLaunch)
                {
                    _build.ReportEvent("hedge");
                    var next = Attempt(execution, tasks.Count, branches.Token).AsTask();
                    tasks.Add(next); pending.Add(next);
                    // 前一次延迟在提前补发时仍可能未结束，统一由 delayCancellation 释放。
                    delay = ResilienceTime.DelayAsync(_build.TimeProvider, _d.Hedging.Delay, delayCancellation.Token);
                }
            }
        }
        finally
        {
            branches.Cancel(); delayCancellation.Cancel();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        return selected ?? await tasks[0].ConfigureAwait(false);
    }

    private async ValueTask<Outcome> Attempt(Execution execution, int index, CancellationToken token)
    {
        IResilienceCircuitLease? circuitTicket = null;
        try
        {
            token.ThrowIfCancellationRequested();
            using var rate = _d.RateLimit.Enabled
                ? await _admission.AcquireRateAsync(_d.Identity, _d.RateLimit, token).ConfigureAwait(false) : null;
            await using var observations = await ResilienceAttemptObservations<T>.BeginAsync(_d.AttemptObservers,
                new(_d.Identity, execution.Context.Fork(token, index)), _build.ReportEvent, token).ConfigureAwait(false);
            circuitTicket = _circuit?.Enter();
            Outcome outcome;
            using (var budget = new Budget(_build.TimeProvider, _d.AttemptTimeout, token))
            {
                try
                {
                    var value = await execution.Callback(execution.Context.Fork(budget.Token, index), budget.Token).ConfigureAwait(false);
                    outcome = new(execution.Record(value), null);
                }
                catch (Exception e) { outcome = new(null, e); }
                if (budget.Expired && !token.IsCancellationRequested)
                {
                    _build.ReportEvent("timeout.attempt");
                    outcome = new(null, new ResilienceTimeoutException(ResilienceTimeoutKind.Attempt, _d.Identity, outcome.Error));
                }
                else if (token.IsCancellationRequested) outcome = new(null, new OperationCanceledException(token));
            }
            if (circuitTicket is not null)
            {
                var excluded = token.IsCancellationRequested || outcome.Error is OperationCanceledException or ResilienceRejectedException or ResilienceCircuitOpenException or ResilienceResultCleanupException or IResilienceScopedRejection or IResilienceNonRecoverableFailure;
                var failure = !excluded && Handle(_d.CircuitBreaker.ShouldHandle!, outcome, execution, index, token, true);
                circuitTicket.Complete(failure, excluded);
                circuitTicket = null;
            }
            await observations.CompleteAsync(outcome.Neutral, token).ConfigureAwait(false);
            return outcome;
        }
        catch (Exception e) { return new(null, e); }
        finally { circuitTicket?.Dispose(); }
    }
    private static bool Handle(Func<ResiliencePredicateArguments<T>, bool> predicate, Outcome outcome,
        Execution execution, int attempt, CancellationToken token, bool excludeRejections)
    {
        if (token.IsCancellationRequested || outcome.Error is OperationCanceledException or ResilienceResultCleanupException or IResilienceNonRecoverableFailure ||
            outcome.Error is ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Execution }) return false;
        if (outcome.Error is IResilienceScopedRejection { CanMoveToNextEndpoint: false }) return false;
        if (excludeRejections && outcome.Error is ResilienceRejectedException or ResilienceCircuitOpenException) return false;
        return predicate(new(outcome.Neutral, execution.Context.Fork(token, attempt), attempt));
    }
    private async ValueTask Discard(Result result)
    {
        if (Interlocked.Exchange(ref result.Discarded, 1) != 0 || _d.DiscardResultAsync is null) return;
        try { await _d.DiscardResultAsync(result.Value).ConfigureAwait(false); }
        catch (Exception error) { throw new ResilienceResultCleanupException(_d.Identity, error); }
    }

    private sealed class Budget : IDisposable
    {
        private readonly CancellationTokenSource _source;
        private readonly ITimer? _timer;
        private int _expired;
        public bool Expired => Volatile.Read(ref _expired) != 0;
        public CancellationToken Token => _source.Token;
        public Budget(TimeProvider clock, TimeSpan? duration, CancellationToken parent)
        {
            _source = CancellationTokenSource.CreateLinkedTokenSource(parent);
            if (duration.HasValue) _timer = clock.CreateTimer(_ =>
            {
                Interlocked.Exchange(ref _expired, 1);
                try { _source.Cancel(); } catch (ObjectDisposedException) { }
            }, null, duration.Value, Timeout.InfiniteTimeSpan);
        }
        public void Dispose() { _timer?.Dispose(); _source.Dispose(); }
    }

}
