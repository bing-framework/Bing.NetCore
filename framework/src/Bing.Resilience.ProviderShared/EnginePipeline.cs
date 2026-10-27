#if BING_FENCES
using Paramore.Fences;
using Paramore.Fences.CircuitBreaker;
using Paramore.Fences.Fallback;
using Paramore.Fences.Hedging;
using Paramore.Fences.RateLimiting;
using Paramore.Fences.Registry;
using Paramore.Fences.Retry;
using Paramore.Fences.Timeout;
using Native = Paramore.Fences;
namespace Bing.Resilience.Internal.Fences;
#else
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Hedging;
using Polly.RateLimiting;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;
using Native = Polly;
namespace Bing.Resilience.Internal.Polly;
#endif

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.RateLimiting;

/// <summary>同一适配实现分别编译进两个可选包，不产生 Provider 之间的运行时引用。</summary>
internal sealed class EngineProvider(string name) : IResilienceRuntimeAwareProvider
{
    public string Name { get; } = name;
    public ResilienceCapabilities Capabilities => ResilienceCapabilities.All;
    public IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context) =>
        new EnginePipeline<T>(definition, context);
}

/// <summary>封装原生策略并集中处理异常、上下文、丢弃结果和引擎生命周期。</summary>
internal sealed class EnginePipeline<T> : IResiliencePipeline<T>
{
    // Box 不实现 IDisposable，禁止原生引擎再次释放其中的业务结果。
    private sealed class Box(T value)
    {
        public T Value { get; } = value;
        public int Discarded;
    }
    private sealed class Execution(
        Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> action,
        ResilienceExecutionContext context)
    {
        public Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> Action { get; } = action;
        public ResilienceExecutionContext Context { get; } = context;
        public ConcurrentBag<Box> Results { get; } = new();
        public ConcurrentDictionary<Exception, byte> BusinessExceptions { get; } = new(ReferenceEqualityComparer.Instance);
        public Box Record(T result) { var box = new Box(result); Results.Add(box); return box; }
    }
    private static readonly Native.ResiliencePropertyKey<Execution> ExecutionKey = new("bing.execution");
    private static readonly Native.ResiliencePropertyKey<int> AttemptKey = new("bing.attempt");
    private readonly Native.ResiliencePipeline<Box> _pipeline;
    private readonly ResiliencePipelineDefinition<T> _definition;
    private readonly ResiliencePipelineBuildContext _build;
    private readonly IResilienceAdmissionResources _admission;

    public EnginePipeline(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext build)
    {
        _definition = definition; _build = build;
        _admission = build.AdmissionResources ?? CreateAdmission();
        var registry = new ResiliencePipelineRegistry<string>();
        build.RegisterResource(registry);
        registry.TryAddBuilder<Box>("pipeline", (builder, _) => Configure(builder));
        _pipeline = registry.GetPipeline<Box>("pipeline");
    }

    private void Configure(Native.ResiliencePipelineBuilder<Box> builder)
    {
        builder.TimeProvider = _build.TimeProvider;
        if (_definition.ExecutionTimeout is { } total)
            AddTimeout(builder, total, ResilienceTimeoutKind.Execution);
        if (_definition.Concurrency.Enabled) builder.AddStrategy(_ => new AdmissionStrategy(this, false));
        builder.AddStrategy(_ => new CleanupStrategy(this));
        if (_definition.Fallback.Enabled)
        {
            builder.AddFallback(new FallbackStrategyOptions<Box>
            {
                ShouldHandle = args => new(Handle(_definition.Fallback.ShouldHandle!, args.Outcome, args.Context, false)),
                OnFallback = async args =>
                {
                    _build.ReportEvent("fallback");
                    if (args.Outcome.Exception is null) await Discard(args.Outcome.Result!).ConfigureAwait(false);
                },
                FallbackAction = async args =>
                {
                    var execution = GetExecution(args.Context);
                    var ctx = execution.Context.Fork(args.Context.CancellationToken, Attempt(args.Context));
                    var value = await _definition.Fallback.Action!(new(ToNeutral(args.Outcome), ctx, args.Context.CancellationToken)).ConfigureAwait(false);
                    return Outcome.FromResult(execution.Record(value));
                }
            });
        }
        if (_definition.Retry.MaxRetryAttempts > 0)
        {
            var r = _definition.Retry;
            builder.AddRetry(new RetryStrategyOptions<Box>
            {
                MaxRetryAttempts = r.MaxRetryAttempts, Delay = r.Delay, MaxDelay = r.MaxDelay,
                BackoffType = DelayBackoffType.Exponential, UseJitter = r.UseJitter,
                ShouldHandle = args => new(Handle(r.ShouldHandle!, args.Outcome, args.Context, true)),
                DelayGenerator = args => new(r.DelayGenerator?.Invoke(Arguments(args.Outcome, args.Context))),
                OnRetry = async args =>
                {
                    _build.ReportEvent("retry");
                    if (args.Outcome.Exception is null) await Discard(args.Outcome.Result!).ConfigureAwait(false);
                    args.Context.Properties.Set(AttemptKey, args.AttemptNumber + 1);
                }
            });
        }
        if (_definition.Hedging.Enabled)
        {
            var h = _definition.Hedging;
            builder.AddHedging(new HedgingStrategyOptions<Box>
            {
                MaxHedgedAttempts = h.MaxHedgedAttempts, Delay = h.Delay,
                ShouldHandle = args => new(Handle(h.ShouldHandle!, args.Outcome, args.Context, false)),
                ActionGenerator = args =>
                {
                    args.ActionContext.Properties.Set(AttemptKey, args.AttemptNumber);
                    return () => args.Callback(args.ActionContext);
                },
                OnHedging = _ => { _build.ReportEvent("hedge"); return default; }
            });
        }
        if (_definition.RateLimit.Enabled) builder.AddStrategy(_ => new AdmissionStrategy(this, true));
        if (_definition.AttemptObservers.Count > 0) builder.AddStrategy(_ => new AttemptObservationStrategy(this));
        if (_definition.CircuitBreaker.Enabled)
        {
            var c = _definition.CircuitBreaker;
            var circuit = (_build.CircuitResources ?? new ResilienceCircuitResources(_build.TimeProvider)).GetCircuit(_definition.Identity,
                new(c.FailureRatio, c.MinimumThroughput, c.SamplingDuration, c.BreakDuration), _build.ReportEvent);
            if (c.EnableManualControl) _build.RegisterCircuit(() => circuit.State, circuit.IsolateAsync, circuit.CloseAsync);
            builder.AddStrategy(_ => new SharedCircuitStrategy(this, circuit));
        }
        if (_definition.AttemptTimeout is { } attempt) AddTimeout(builder, attempt, ResilienceTimeoutKind.Attempt);
    }

    private void AddTimeout(Native.ResiliencePipelineBuilder<Box> builder, TimeSpan timeout, ResilienceTimeoutKind kind)
    {
#if NET8_0_OR_GREATER
        builder.AddStrategy(_ => new ExceptionMapper(_build, timeout: kind));
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Name = kind.ToString(), Timeout = timeout,
            OnTimeout = _ => { _build.ReportEvent(kind == ResilienceTimeoutKind.Execution ? "timeout.execution" : "timeout.attempt"); return default; }
        });
#else
        builder.AddStrategy(_ => new OwnedTimeoutStrategy(this, timeout, kind));
#endif
    }

    // net6 的 BCL TimeProvider CTS 兼容实现不会在 CTS.Dispose 时释放自定义 timer。
    // 显式拥有 timer，保留中性超时及取消优先级，避免长寿命管线积累定时器。
#if !NET8_0_OR_GREATER
    private sealed class OwnedTimeoutStrategy(EnginePipeline<T> owner, TimeSpan timeout, ResilienceTimeoutKind kind) : Native.ResilienceStrategy<Box>
    {
        protected override async ValueTask<Outcome<Box>> ExecuteCore<TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<Box>>> callback, Native.ResilienceContext context, TState state)
        {
            var parent = context.CancellationToken;
            using var source = CancellationTokenSource.CreateLinkedTokenSource(parent);
            var expired = 0;
            using var timer = owner._build.TimeProvider.CreateTimer(_ =>
            {
                Interlocked.Exchange(ref expired, 1);
                try { source.Cancel(); } catch (ObjectDisposedException) { } catch (AggregateException) { }
            }, null, timeout, System.Threading.Timeout.InfiniteTimeSpan);
            Outcome<Box> outcome;
            var child = Native.ResilienceContextPool.Shared.Get(source.Token);
            child.Properties.Set(ExecutionKey, GetExecution(context));
            child.Properties.Set(AttemptKey, Attempt(context));
            try { outcome = await callback(child, state).ConfigureAwait(false); }
            catch (Exception error) { outcome = Outcome.FromException<Box>(error); }
            finally { Native.ResilienceContextPool.Shared.Return(child); }
            if (parent.IsCancellationRequested) return Outcome.FromException<Box>(new OperationCanceledException(parent));
            if (Volatile.Read(ref expired) != 0 && outcome.Exception is not ResilienceResultCleanupException)
            {
                if (outcome.Exception is null) await owner.Discard(outcome.Result!).ConfigureAwait(false);
                owner._build.ReportEvent(kind == ResilienceTimeoutKind.Execution ? "timeout.execution" : "timeout.attempt");
                return Outcome.FromException<Box>(new ResilienceTimeoutException(kind, owner._build.Identity, outcome.Exception));
            }
            return outcome;
        }
    }
#endif

    private IResilienceAdmissionResources CreateAdmission()
    {
        var resources = new ResilienceAdmissionResources(_build.TimeProvider);
        _build.RegisterResource(resources); return resources;
    }

    /// <summary>准入由 Core 持有；原生引擎只负责策略调度，切换不重置配额。</summary>
    private sealed class AdmissionStrategy(EnginePipeline<T> owner, bool rate) : Native.ResilienceStrategy<Box>
    {
        protected override async ValueTask<Outcome<Box>> ExecuteCore<TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<Box>>> callback, Native.ResilienceContext context, TState state)
        {
            try
            {
                using var lease = rate
                    ? await owner._admission.AcquireRateAsync(owner._definition.Identity, owner._definition.RateLimit, context.CancellationToken).ConfigureAwait(false)
                    : await owner._admission.AcquireConcurrencyAsync(owner._definition.Identity, owner._definition.Concurrency, context.CancellationToken).ConfigureAwait(false);
                return await callback(context, state).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (error is ResilienceRejectedException) owner._build.ReportEvent(rate ? "rejected.rate" : "rejected.concurrency");
                return Outcome.FromException<Box>(error);
            }
        }
    }

    private sealed class AttemptObservationStrategy(EnginePipeline<T> owner) : Native.ResilienceStrategy<Box>
    {
        protected override async ValueTask<Outcome<Box>> ExecuteCore<TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<Box>>> callback, Native.ResilienceContext context, TState state)
        {
            try
            {
                await using var observations = await ResilienceAttemptObservations<T>.BeginAsync(owner._definition.AttemptObservers,
                    new(owner._definition.Identity, GetExecution(context).Context.Fork(context.CancellationToken, Attempt(context))),
                    owner._build.ReportEvent, context.CancellationToken).ConfigureAwait(false);
                var outcome = await callback(context, state).ConfigureAwait(false);
                await observations.CompleteAsync(ToNeutral(outcome), context.CancellationToken).ConfigureAwait(false);
                return outcome;
            }
            catch (Exception error) { return Outcome.FromException<Box>(error); }
        }
    }

    private sealed class SharedCircuitStrategy(EnginePipeline<T> owner, IResilienceSharedCircuit circuit) : Native.ResilienceStrategy<Box>
    {
        protected override async ValueTask<Outcome<Box>> ExecuteCore<TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<Box>>> callback, Native.ResilienceContext context, TState state)
        {
            try
            {
                using var lease = circuit.Enter();
                var outcome = await callback(context, state).ConfigureAwait(false);
                var excluded = context.CancellationToken.IsCancellationRequested ||
                    outcome.Exception is OperationCanceledException or ResilienceRejectedException or ResilienceCircuitOpenException or ResilienceResultCleanupException or IResilienceScopedRejection or IResilienceNonRecoverableFailure;
                var failure = !excluded && Handle(owner._definition.CircuitBreaker.ShouldHandle!, outcome, context, true);
                lease.Complete(failure, excluded);
                return outcome;
            }
            catch (Exception error) { return Outcome.FromException<Box>(error); }
        }
    }

    private static Execution GetExecution(Native.ResilienceContext context) => context.Properties.GetValue(ExecutionKey, null!)!;
    private static int Attempt(Native.ResilienceContext context) => context.Properties.GetValue(AttemptKey, 0);
    private static ResilienceOutcome<T> ToNeutral(Outcome<Box> outcome) => outcome.Exception is null
        ? ResilienceOutcome<T>.FromResult(outcome.Result!.Value) : ResilienceOutcome<T>.FromException(outcome.Exception);
    private static ResiliencePredicateArguments<T> Arguments(Outcome<Box> outcome, Native.ResilienceContext context) =>
        new(ToNeutral(outcome), GetExecution(context).Context.Fork(context.CancellationToken, Attempt(context)), Attempt(context));
    private static bool Handle(Func<ResiliencePredicateArguments<T>, bool> predicate, Outcome<Box> outcome, Native.ResilienceContext context, bool excludeRejections)
    {
        if (context.CancellationToken.IsCancellationRequested || outcome.Exception is OperationCanceledException or ResilienceResultCleanupException or IResilienceNonRecoverableFailure ||
            outcome.Exception is ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Execution })
            return false;
        if (outcome.Exception is IResilienceScopedRejection { CanMoveToNextEndpoint: false }) return false;
        if (excludeRejections && outcome.Exception is ResilienceRejectedException or ResilienceCircuitOpenException) return false;
        return predicate(Arguments(outcome, context));
    }
    private async ValueTask Discard(Box box)
    {
        if (Interlocked.Exchange(ref box.Discarded, 1) == 0 && _definition.DiscardResultAsync is { } discard)
        {
            try { await discard(box.Value).ConfigureAwait(false); }
            catch (Exception error) { throw new ResilienceResultCleanupException(_build.Identity, error); }
        }
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
        var execution = new Execution(action, context.Fork(cancellationToken, nestedExecution: true));
        var native = Native.ResilienceContextPool.Shared.Get(cancellationToken);
        native.Properties.Set(ExecutionKey, execution);
        native.Properties.Set(AttemptKey, 0);
        Box? delivered = null;
        try
        {
            var outcome = await _pipeline.ExecuteOutcomeAsync(static async (ctx, state) =>
            {
                try
                {
                    var value = await state.Action(state.Context.Fork(ctx.CancellationToken, Attempt(ctx)), ctx.CancellationToken).ConfigureAwait(false);
                    return Outcome.FromResult(state.Record(value));
                }
                catch (Exception e) { state.BusinessExceptions.TryAdd(e, 0); return Outcome.FromException<Box>(e); }
            }, native, execution).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (outcome.Exception is not null) ExceptionDispatchInfo.Capture(outcome.Exception).Throw();
            delivered = outcome.Result!;
            return delivered.Value;
        }
        finally
        {
            // 原生 Hedging 在完成前等待取消分支；因此此处能够完整收集所有迟到结果。
            List<Exception>? failures = null;
            foreach (var result in execution.Results)
                if (!ReferenceEquals(result, delivered))
                    try { await Discard(result).ConfigureAwait(false); } catch (Exception e) { (failures ??= new()).Add(e); }
            Native.ResilienceContextPool.Shared.Return(native);
            if (failures is not null)
            {
                if (delivered is not null)
                    try { await Discard(delivered).ConfigureAwait(false); } catch (Exception e) { failures.Add(e); }
                throw new ResilienceResultCleanupException(_build.Identity, new AggregateException(failures));
            }
        }
    }


    /// <summary>在逻辑并发许可内部完成分支清理，避免清理期间提前归还许可。</summary>
    private sealed class CleanupStrategy(EnginePipeline<T> owner) : Native.ResilienceStrategy<Box>
    {
        protected override async ValueTask<Outcome<Box>> ExecuteCore<TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<Box>>> callback,
            Native.ResilienceContext context, TState state)
        {
            Outcome<Box> outcome;
            try { outcome = await callback(context, state).ConfigureAwait(false); }
            catch (Exception e) { outcome = Outcome.FromException<Box>(e); }
            var execution = GetExecution(context);
            List<Exception>? failures = null;
            var selected = outcome.Exception is null ? outcome.Result : null;
            foreach (var result in execution.Results)
                if (!ReferenceEquals(result, selected))
                    try { await owner.Discard(result).ConfigureAwait(false); }
                    catch (Exception e) { (failures ??= new()).Add(e); }
            if (failures is not null || context.CancellationToken.IsCancellationRequested)
            {
                if (selected is not null)
                    try { await owner.Discard(selected).ConfigureAwait(false); }
                    catch (Exception e) { (failures ??= new()).Add(e); }
                if (failures is not null) return Outcome.FromException<Box>(new ResilienceResultCleanupException(owner._build.Identity, new AggregateException(failures)));
                return Outcome.FromException<Box>(new OperationCanceledException(context.CancellationToken));
            }
            return outcome;
        }
    }

    /// <summary>位于对应原生策略外侧，保证外部重试/熔断谓词看到中性错误。</summary>
    private sealed class ExceptionMapper(ResiliencePipelineBuildContext build,
        ResilienceTimeoutKind? timeout = null, ResilienceRejectionReason? rejection = null, bool circuit = false) : Native.ResilienceStrategy
    {
        protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
            Func<Native.ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback, Native.ResilienceContext context, TState state)
        {
            var outcome = await callback(context, state).ConfigureAwait(false);
            var error = outcome.Exception;
            if (error is null || GetExecution(context).BusinessExceptions.ContainsKey(error)) return outcome;
            Exception? mapped = error switch
            {
                TimeoutRejectedException when timeout.HasValue => new ResilienceTimeoutException(timeout.Value, build.Identity, error),
                RateLimiterRejectedException when rejection.HasValue => new ResilienceRejectedException(rejection.Value, build.Identity, error),
                BrokenCircuitException when circuit => new ResilienceCircuitOpenException(build.Identity, error),
                _ => null
            };
            return mapped is null ? outcome : Outcome.FromException<TResult>(mapped);
        }
    }
}
