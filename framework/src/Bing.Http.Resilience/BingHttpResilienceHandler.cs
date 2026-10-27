using Bing.Resilience;

namespace Bing.Http.Resilience;

/// <summary>外层一次逻辑预算/并发，内层选择恢复策略；所有真实发送都经过内侧 handler。</summary>
internal sealed class BingHttpResilienceHandler(
    BingHttpResilienceExtensions.Registration registration, IResiliencePipelineProvider pipelines, TimeProvider clock, IBingHttpRoutingStrategy? routing = null, IBingHttpReplayStrategy? replay = null) : DelegatingHandler
{
    private IResiliencePipeline<HttpResponseMessage> Pipeline(string purpose, string partition = "") =>
        pipelines.GetPipeline<HttpResponseMessage>(registration.ClientName, purpose, partition);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var state = new BingHttpRequestState(request, registration.Options, clock);
        var lease = pipelines is IResilienceRuntimeLeaseProvider { IsRuntimeSwitchingEnabled: true } runtime
            ? runtime.AcquireLease("http:" + registration.ClientName) : null;
        if (lease is not null) ResilienceDurableAttemptScope.Current?.ObserveGeneration(lease.Version);
        var context = lease?.Context ?? new ResilienceExecutionContext();
        context.Set(BingHttpRequestState.Key, state);
        try
        {
        var result = await Pipeline("http.send").ExecuteAsync(async (outer, token) =>
        {
            using var replaySnapshot = replay is null ? null : await replay.CaptureAsync(request, token).ConfigureAwait(false);
            if (replaySnapshot is not null) state.EnableWriteReplay();
            var route = registration.Options.UseDynamicRouting ? await routing!.AcquireAsync(request, outer, token).ConfigureAwait(false) : null;
            try
            {
                async ValueTask<HttpResponseMessage> Schedule() => state.Hedge
                    ? await Pipeline("http.hedge").ExecuteAsync(SendAttempt, outer, token).ConfigureAwait(false)
                    : state.CanRetry
                        ? await Pipeline("http.retry").ExecuteAsync(SendAttempt, outer, token).ConfigureAwait(false)
                        : await SendAttempt(outer, token).ConfigureAwait(false);
                var (response, synthetic) = await HttpSyntheticResponseExecutor.Execute(request, replaySnapshot?.BodyFingerprint,
                    registration.Options, clock, Schedule, token).ConfigureAwait(false);
                if (synthetic is not null) HttpSyntheticDiagnostics.Record(registration.ClientName, synthetic.Kind);
                if (registration.Options.ResponseObserver is { } observer)
                {
                    request.Options.TryGetValue(BingHttpOperationMetadata.Operation, out var operation);
                    try
                    {
                        await observer.ObserveAsync(new(registration.ClientName, operation, response.StatusCode, synthetic is not null,
                            synthetic?.Kind, synthetic?.SourceId), token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // 诊断观察器不拥有响应，也不能改变已经选定的业务结果。
                        ResilienceDiagnostics.RecordEvent(new ResiliencePipelineIdentity("http", registration.ClientName,
                            typeof(HttpResponseMessage)), "http", "response.observe_failed");
                    }
                }
                if (route is not null) { response.Content = new GenerationHttpContent(response.Content, route); route = null; }
                return response;
            }
            finally { route?.Dispose(); }

            async ValueTask<HttpResponseMessage> SendAttempt(ResilienceExecutionContext attemptContext, CancellationToken ct)
            {
                var attemptIndex = attemptContext.AttemptNumber;
                // 在配额、端点许可和单次预算之前等待；只消耗完整调用预算。
                await state.WaitForServerAsync(ct).ConfigureAwait(false);
                return await Pipeline("http.rate").ExecuteAsync(async (rateContext, rateToken) =>
                {
                    var dynamicEndpoint = route?.Select(attemptIndex);
                    var endpoint = dynamicEndpoint is null && state.Hedge ? registration.Options.Hedging.Endpoints[attemptIndex % registration.Options.Hedging.Endpoints.Count] : null;
                    var attempt = dynamicEndpoint is not null ? route!.GetPipeline(dynamicEndpoint) : endpoint is null ? Pipeline("http.attempt") : Pipeline("http.endpoint", endpoint.Id);
                    try
                    {
                    return await attempt.ExecuteAsync(async (_, sendToken) =>
                    {
                        HttpRequestMessage? clone = null;
                        try
                        {
                            if (dynamicEndpoint is not null) route!.ValidateBeforeSend(dynamicEndpoint);
                            var origin = dynamicEndpoint?.Origin ?? endpoint?.Origin;
                            ResilienceDurableAttemptScope.Current?.ObserveEndpoint(dynamicEndpoint?.Id ?? endpoint?.Id ?? registration.ClientName);
                            var sending = replaySnapshot is not null
                                ? clone = await replaySnapshot.CreateRequestAsync(origin, sendToken).ConfigureAwait(false)
                                : origin is null ? request : clone = state.Clone(origin);
                            var response = await base.SendAsync(sending, sendToken).ConfigureAwait(false);
                            state.Observe(response);
                            return response;
                        }
                        finally { clone?.Dispose(); }
                    }, rateContext, rateToken).ConfigureAwait(false);
                    }
                    catch (ResilienceCircuitOpenException error) when (dynamicEndpoint is not null || endpoint is not null)
                    { throw new BingHttpEndpointUnavailableException(dynamicEndpoint?.Id ?? endpoint!.Id, error); }
                    catch (ResilienceRejectedException error) when ((dynamicEndpoint is not null || endpoint is not null) && error.Reason == ResilienceRejectionReason.Concurrency)
                    { throw new BingHttpEndpointUnavailableException(dynamicEndpoint?.Id ?? endpoint!.Id, error); }
                }, attemptContext, ct).ConfigureAwait(false);
            }
        }, context, cancellationToken).ConfigureAwait(false);
        if (lease is not null) { result.Content = new GenerationHttpContent(result.Content, lease); lease = null; }
        return result;
        }
        finally { lease?.Dispose(); }
    }
}

/// <summary>一次逻辑 HTTP 调用的快照和并发安全的服务端等待期限。</summary>
internal sealed class BingHttpRequestState
{
    internal static readonly ResiliencePropertyKey<BingHttpRequestState> Key = new("http.state", state => state);
    private readonly HttpRequestMessage _request;
    private readonly BingHttpResilienceOptions _options;
    private readonly TimeProvider _clock;
    private readonly KeyValuePair<string, string[]>[] _headers;
    private readonly KeyValuePair<string, object?>[] _values;
    private long _notBefore;
    internal bool CanRetry { get; private set; }
    internal void EnableWriteReplay() => CanRetry = true;
    internal bool Hedge { get; }

    internal BingHttpRequestState(HttpRequestMessage request, BingHttpResilienceOptions options, TimeProvider clock)
    {
        _request = request; _options = options; _clock = clock;
        var durable = ResilienceDurableAttemptScope.Current is not null || request.Options.TryGetValue(BingHttpOperationMetadata.DurableAttempt, out var value) && value;
        var stream = request.Options.TryGetValue(BingHttpOperationMetadata.IsStreamResult, out var isStream) && isStream;
        var eligible = !durable && !stream && request.Content is null && (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head);
        var sequential = options.Mode == BingHttpResilienceMode.Retry || options.UseSequentialRetry?.Invoke(request) == true;
        CanRetry = eligible && sequential && options.CanRetryRequest?.Invoke(request) == true;
        Hedge = eligible && !sequential && options.CanHedgeRequest?.Invoke(request) == true;
        _headers = Array.Empty<KeyValuePair<string, string[]>>();
        _values = Array.Empty<KeyValuePair<string, object?>>();
        if (!Hedge && !options.UseDynamicRouting) return;
        if (!options.UseDynamicRouting && (request.RequestUri is null || !request.RequestUri.IsAbsoluteUri ||
            !string.Equals(request.RequestUri.GetLeftPart(UriPartial.Authority),
                options.Hedging.Endpoints[0].Origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)))
            throw new ResilienceConfigurationException("Hedging 原请求必须属于地址池的主 Origin。");
        _headers = request.Headers.Where(h => options.Hedging.ForwardHeaders.Contains(h.Key) && !options.Hedging.ForbiddenHeader(h.Key))
            .Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray())).ToArray();
        _values = request.Options.ToArray();
        foreach (var pair in _values)
            if (!IsImmutableValue(pair.Value) &&
                !options.Hedging.OptionCopiers.ContainsKey(pair.Key))
                throw new ResilienceConfigurationException($"Hedging Options '{pair.Key}' 必须登记复制器。");
    }
    private static bool IsImmutableValue(object? value) => value is null or string or CancellationToken or Guid or decimal or DateTime or DateTimeOffset or TimeSpan or ResilienceOperationDescriptor ||
        value.GetType().IsPrimitive || value.GetType().IsEnum;

    internal static BingHttpRequestState From(ResilienceExecutionContext context) =>
        context.TryGet(Key, out var state) ? state : throw new InvalidOperationException("缺少 HTTP 执行上下文。");

    internal HttpRequestMessage Clone(Uri origin)
    {
        // 使用绝对 origin 拼接原始 PathAndQuery，避免以 // 开头的路径覆盖路由。
        var uri = new Uri(origin.GetLeftPart(UriPartial.Authority) + _request.RequestUri!.PathAndQuery, UriKind.Absolute);
        var clone = new HttpRequestMessage(_request.Method, uri) { Version = _request.Version, VersionPolicy = _request.VersionPolicy };
        try
        {
            if (_request.Content is not null) clone.Content = new BorrowedRequestContent(_request.Content);
            foreach (var header in _headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            foreach (var pair in _values)
            {
                var value = _options.Hedging.OptionCopiers.TryGetValue(pair.Key, out var copy) ? copy(pair.Value) : pair.Value;
                clone.Options.Set(new HttpRequestOptionsKey<object?>(pair.Key), value);
            }
            return clone;
        }
        catch { clone.Dispose(); throw; }
    }
    internal TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (retryAfter?.Date is { } date)
        {
            var delay = date - _clock.GetUtcNow();
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }
        return null;
    }
    internal void Observe(HttpResponseMessage response)
    {
        if (!Hedge || !HttpFailureClassifier.IsTransient(ResilienceOutcome<HttpResponseMessage>.FromResult(response), true)) return;
        if (RetryAfter(response) is not { } delay || delay <= TimeSpan.Zero) return;
        var now = _clock.GetTimestamp();
        var ticks = delay.TotalSeconds * _clock.TimestampFrequency;
        var deadline = ticks >= long.MaxValue - now ? long.MaxValue : now + (long)ticks;
        long current;
        do { current = Volatile.Read(ref _notBefore); if (deadline <= current) return; }
        while (Interlocked.CompareExchange(ref _notBefore, deadline, current) != current);
    }
    internal async ValueTask WaitForServerAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var deadline = Volatile.Read(ref _notBefore);
            var remaining = (deadline - (double)_clock.GetTimestamp()) / _clock.TimestampFrequency;
            if (remaining <= 0) return;
            var delay = TimeSpan.FromSeconds(Math.Min(remaining, 86400));
#if NET8_0_OR_GREATER
            await Task.Delay(delay, _clock, token).ConfigureAwait(false);
#else
            await _clock.Delay(delay, token).ConfigureAwait(false);
#endif
        }
    }
}

/// <summary>端点级拒绝消耗一次候选名额；全局并发/速率拒绝不转换。</summary>
internal sealed class BingHttpEndpointUnavailableException(string endpointId, Exception inner)
    : Exception("候选端点当前不接收尝试。", inner), IResilienceScopedRejection
{
    public string EndpointId { get; } = endpointId;
    public bool CanMoveToNextEndpoint => true;
}
