using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bing.Resilience;
using Bing.Resilience.Compensation;
using Bing.Resilience.Discovery;
using Bing.Resilience.Distributed;
using Bing.Resilience.Operations;
using Bing.Resilience.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

[CollectionDefinition("Resilience lifecycle diagnostics", DisableParallelization = true)]
public sealed class LifecycleDiagnosticsCollection
{
    public const string Name = "Resilience lifecycle diagnostics";
}

[Collection(LifecycleDiagnosticsCollection.Name)]
public sealed class LifecycleDiagnosticsTests
{
    [Fact]
    public async Task RuntimeSwitchReportsSuccessFailureCancellationAndDrain()
    {
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddFencesProvider("fences")
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching(options => options.DrainTimeout = TimeSpan.FromMilliseconds(30));
        services.AddBingResiliencePipeline<int>("work", options => options.SwitchGroup = "diagnostic-group");
        using var host = services.BuildServiceProvider();
        var pipelines = host.GetRequiredService<IResiliencePipelineProvider>();
        var leases = (IResilienceRuntimeLeaseProvider)pipelines;
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        using var capture = new DiagnosticCapture();

        var lease = leases.AcquireLease("diagnostic-group");
        try
        {
            var switched = await runtime.SwitchAsync("diagnostic-group", "fences", 0);
            Assert.False(switched.PreviousGenerationDrained);
        }
        finally
        {
            lease.Dispose();
        }

        await Assert.ThrowsAsync<ResilienceConfigurationException>(
            () => runtime.SwitchAsync("diagnostic-group", "missing-provider", 1).AsTask());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runtime.SwitchAsync("diagnostic-group", "builtin", 1, cancelled.Token).AsTask());

        AssertLifecycleCount(capture, "generation.switch", "success", 1);
        AssertLifecycleCount(capture, "generation.switch", "failure", 1);
        AssertLifecycleCount(capture, "generation.switch", "cancelled", 1);
        AssertLifecycleCount(capture, "generation.drain", "pending", 1);
        AssertLifecycleCount(capture, "generation.drain", "success", 1);
        AssertBoundedTags(capture, "diagnostic-group", "missing-provider");
    }

    [Fact]
    public async Task DiscoveryReportsRefreshFailureAndServedStaleSnapshot()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var source = new ConfigurationServiceEndpointSource(
            new DelegateServiceEndpointTrustPolicy((service, endpoint) =>
                service == "catalog" && endpoint.Origin.Host.EndsWith(".test", StringComparison.Ordinal)),
            clock);
        using var capture = new DiagnosticCapture();

        source.Publish(Snapshot(clock, "source-version-sensitive"), null);
        Assert.Throws<ResilienceConfigurationException>(() =>
            source.Publish(Snapshot(clock, "next-version"), "stale-expected-version"));

        clock.Advance(TimeSpan.FromSeconds(11));
        var stale = await source.GetSnapshotAsync("catalog");

        Assert.Equal("source-version-sensitive", stale.SourceVersion);
        AssertLifecycleCount(capture, "discovery.refresh", "success", 1);
        AssertLifecycleCount(capture, "discovery.refresh", "failure", 1);
        AssertLifecycleCount(capture, "discovery.stale", "served", 1);
        AssertBoundedTags(capture, "catalog", "https://address-sensitive.test/", "operation-id-sensitive", "token-sensitive", "source-version-sensitive");
    }

    [Fact]
    public async Task DistributedAdmissionReportsAllowedRejectedFailureAndCancellation()
    {
        var configuration = new DistributedCircuitConfiguration("version-sensitive", .5, 2, 30000, 15000, 1000);
        var context = new ResilienceAttemptContext(
            new("http.endpoint", "operation-id-sensitive", typeof(int), "https://address-sensitive.test/version-sensitive"), new());
        using var capture = new DiagnosticCapture();

        var allowedStore = new DelegateDistributedStore((key, config, attemptId, _) =>
            ValueTask.FromResult(new DistributedCircuitAdmission(true, key, attemptId, 1, config.Version,
                "token-sensitive", null, TimeSpan.Zero)));
        var allowedObserver = Observer(allowedStore, configuration);
        await using (var observation = await allowedObserver.BeginAsync(context, default))
            await observation.CompleteAsync(ResilienceOutcome<int>.FromResult(1), default);
        Assert.Equal(DistributedCircuitOutcome.Success, allowedStore.LastOutcome!.Value);

        var rejectedStore = new DelegateDistributedStore((key, config, attemptId, _) =>
            ValueTask.FromResult(new DistributedCircuitAdmission(false, key, attemptId, 1, config.Version,
                "token-sensitive", null, TimeSpan.FromSeconds(1))));
        await Assert.ThrowsAsync<DistributedCircuitRejectedException>(
            () => Observer(rejectedStore, configuration).BeginAsync(context, default).AsTask());

        var failingStore = new DelegateDistributedStore((_, _, _, _) =>
            ValueTask.FromException<DistributedCircuitAdmission>(new IOException("store unavailable")));
        await Assert.ThrowsAsync<ResilienceStateStoreUnavailableException>(
            () => Observer(failingStore, configuration).BeginAsync(context, default).AsTask());

        var waitingStore = new DelegateDistributedStore(async (key, config, attemptId, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new DistributedCircuitAdmission(true, key, attemptId, 1, config.Version, null, null, TimeSpan.Zero);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Observer(waitingStore, configuration).BeginAsync(context, cancellation.Token).AsTask());

        AssertLifecycleCount(capture, "distributed.admission", "allowed", 1);
        AssertLifecycleCount(capture, "distributed.admission", "rejected", 1);
        AssertLifecycleCount(capture, "distributed.admission", "failure", 1);
        AssertLifecycleCount(capture, "distributed.admission", "cancelled", 1);
        AssertBoundedTags(capture, "https://address-sensitive.test/version-sensitive", "operation-id-sensitive", "token-sensitive", "version-sensitive", "circuit-token-sensitive");
    }

    [Fact]
    public async Task ReplayAndSagaDiagnosticsFollowCommittedSqliteState()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        using var capture = new DiagnosticCapture();

        var replayOperation = DurableWorkflowTestSupport.WriteOperation("diagnostics.replay");
        var replayServices = DurableWorkflowTestSupport.CreateServices(database, replayOperation);
        replayServices.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(true),
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", replayOperation.Name, new SuccessfulReplayHandler()));
        using var replayHost = DurableWorkflowTestSupport.Build(replayServices);
        var replay = replayHost.Services.GetRequiredService<IReplayCoordinator>();
        var replayCommand = new ReplayCommand("operation-id-sensitive", replayOperation.Name, "v1", "orders", "1",
            "tenant-a", "idempotency-key", "payload", DateTimeOffset.UtcNow.AddHours(1));
        await replay.EnqueueAsync(replayCommand);
        Assert.True(await replayHost.Services.GetRequiredService<ReplayWorker>().RunOnceAsync());
        Assert.Equal("Succeeded", (await replay.GetAsync(replayCommand.OperationId, replayCommand.AccessScope))!.State);

        var sagaOperation = DurableWorkflowTestSupport.WriteOperation("diagnostics.saga");
        var sagaServices = DurableWorkflowTestSupport.CreateServices(database, sagaOperation);
        sagaServices.AddBingSaga(options =>
        {
            options.Authorize = (_, _) => ValueTask.FromResult(true);
            options.RetryDelay = TimeSpan.Zero;
        }, DurableWorkflowTestSupport.SagaHandler("handler", "1", new SuccessfulSagaHandler()));
        using var sagaHost = DurableWorkflowTestSupport.Build(sagaServices);
        var saga = sagaHost.Services.GetRequiredService<ISagaCoordinator>();
        var sagaCommand = new SagaCommand("saga-id-sensitive", sagaOperation.Name, "v1", "tenant-a", "saga-key",
            new[] { new SagaStepDefinition("write", "handler", "1", "input") }, DateTimeOffset.UtcNow.AddHours(1));
        await saga.StartAsync(sagaCommand);
        var worker = sagaHost.Services.GetRequiredService<SagaWorker>();
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("Completed", (await saga.GetAsync(sagaCommand.SagaId, sagaCommand.AccessScope))!.Saga.State);

        AssertLifecycleCount(capture, "replay.state", "accepted", 1);
        AssertLifecycleCount(capture, "replay.state", "Executing", 1);
        AssertLifecycleCount(capture, "replay.state", "Succeeded", 1);
        AssertLifecycleCount(capture, "saga.state", "accepted", 1);
        AssertLifecycleCount(capture, "saga.state", "Running", 2);
        AssertLifecycleCount(capture, "saga.state", "Completed", 1);
        AssertBoundedTags(capture, "operation-id-sensitive", "saga-id-sensitive", "tenant-a", "saga-key", "input", "diagnostics.replay", "diagnostics.saga", "v1");
    }

    [Fact]
    public async Task ThrowingDiagnosticListenersDoNotChangePublishedBusinessState()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var source = new ConfigurationServiceEndpointSource(
            new DelegateServiceEndpointTrustPolicy((_, _) => true), clock);
        using var capture = new DiagnosticCapture(throwFromCallbacks: true);

        source.Publish(Snapshot(clock, "stable-version"), null);
        var snapshot = await source.GetSnapshotAsync("catalog");

        Assert.Equal("stable-version", snapshot.SourceVersion);
        Assert.Single(snapshot.Endpoints);
    }

    private static ServiceEndpointSnapshot Snapshot(TimeProvider clock, string version)
    {
        var now = clock.GetUtcNow();
        return new ServiceEndpointSnapshot("catalog", version, now, now.AddSeconds(10), now.AddMinutes(2),
            new[] { new ServiceEndpoint("node-a", "incarnation-1", new Uri("https://address-sensitive.test/"),
                "contract-v1", "catalog-group", "catalog", 1) });
    }

    private static DistributedCircuitObserver<int> Observer(IDistributedCircuitStateStore store,
        DistributedCircuitConfiguration configuration) => new(store, new()
    {
        CircuitKey = "circuit-token-sensitive",
        Configuration = configuration,
        FailureMode = DistributedCircuitFailureMode.FailClosed,
        StoreTimeout = TimeSpan.FromSeconds(2)
    }, outcome => outcome.Exception is not null);

    private static void AssertLifecycleCount(DiagnosticCapture capture, string eventName, string outcome, int count)
    {
        Assert.True(count == capture.Measurements.Count(item => item.EventName == eventName && item.Outcome == outcome), $"Expected {eventName}/{outcome}={count}; observed: " + string.Join(",", capture.Measurements.Select(x => x.EventName + "/" + x.Outcome)));
        Assert.Equal(count, capture.Activities.Count(item => item.Name == eventName && item.Outcome == outcome));
    }

    private static void AssertBoundedTags(DiagnosticCapture capture, params string[] sensitiveValues)
    {
        foreach (var item in capture.Measurements)
        {
            Assert.Equal(new[] { "resilience.event", "resilience.outcome" }, item.Tags.Keys.OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(item.EventName, item.Tags["resilience.event"]?.ToString());
            Assert.Equal(item.Outcome, item.Tags["resilience.outcome"]?.ToString());
            AssertNoSensitiveValue(item.Tags.Values, sensitiveValues);
        }

        foreach (var item in capture.Activities)
        {
            Assert.Equal(new[] { "resilience.outcome" }, item.Tags.Keys.OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(item.Outcome, item.Tags["resilience.outcome"]?.ToString());
            AssertNoSensitiveValue(new object?[] { item.Name }, sensitiveValues);
            AssertNoSensitiveValue(item.Tags.Values, sensitiveValues);
        }
    }

    private static void AssertNoSensitiveValue(IEnumerable<object?> values, IEnumerable<string> sensitiveValues)
    {
        foreach (var value in values)
        foreach (var sensitive in sensitiveValues)
            Assert.DoesNotContain(sensitive, value?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed record MetricEvent(string EventName, string Outcome, IReadOnlyDictionary<string, object?> Tags);
    private sealed record ActivityEvent(string Name, string Outcome, IReadOnlyDictionary<string, object?> Tags);

    private sealed class DiagnosticCapture : IDisposable
    {
        private readonly ConcurrentQueue<MetricEvent> _measurements = new();
        private readonly ConcurrentQueue<ActivityEvent> _activities = new();
        private readonly MeterListener _meterListener;
        private readonly ActivityListener _activityListener;

        public DiagnosticCapture(bool throwFromCallbacks = false)
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == ResilienceDiagnostics.SourceName && instrument.Name == "bing.resilience.events")
                        listener.EnableMeasurementEvents(instrument);
                }
            };
            _meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                if (throwFromCallbacks)
                {
                    // 故障监听器主动撤订，避免测试在全局 Meter 中遗留故障回调。
                    _meterListener!.DisableMeasurementEvents(instrument);
                    throw new InvalidOperationException("diagnostic listener failure");
                }
                var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in tags) values[tag.Key] = tag.Value;
                _measurements.Enqueue(new MetricEvent(
                    values["resilience.event"]?.ToString() ?? string.Empty,
                    values["resilience.outcome"]?.ToString() ?? string.Empty,
                    values));
            });
            _meterListener.Start();

            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ResilienceDiagnostics.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity =>
                {
                    if (throwFromCallbacks)
                    {
                        _activityListener!.Dispose();
                        throw new InvalidOperationException("activity listener failure");
                    }
                    var values = activity.Tags.ToDictionary(tag => tag.Key, tag => (object?)tag.Value, StringComparer.Ordinal);
                    _activities.Enqueue(new ActivityEvent(activity.OperationName,
                        values.GetValueOrDefault("resilience.outcome")?.ToString() ?? string.Empty, values));
                }
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        public IReadOnlyCollection<MetricEvent> Measurements => _measurements.ToArray();
        public IReadOnlyCollection<ActivityEvent> Activities => _activities.ToArray();

        public void Dispose()
        {
            _meterListener.Dispose();
            _activityListener.Dispose();
        }
    }

    private sealed class DelegateDistributedStore(
        Func<string, DistributedCircuitConfiguration, string, CancellationToken, ValueTask<DistributedCircuitAdmission>> admit)
        : IDistributedCircuitStateStore
    {
        public DistributedCircuitOutcome? LastOutcome { get; private set; }

        public ValueTask<DistributedCircuitAdmission> AdmitAsync(string key, DistributedCircuitConfiguration configuration,
            string attemptId, CancellationToken cancellationToken = default) => admit(key, configuration, attemptId, cancellationToken);

        public ValueTask ReportAsync(DistributedCircuitAdmission admission, DistributedCircuitConfiguration configuration,
            DistributedCircuitOutcome outcome, CancellationToken cancellationToken = default)
        {
            LastOutcome = outcome;
            return ValueTask.CompletedTask;
        }

        public ValueTask<DistributedCircuitSnapshot> GetSnapshotAsync(string key, DistributedCircuitConfiguration configuration,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(new DistributedCircuitSnapshot(
                ResilienceCircuitState.Closed, 0, configuration.Version, DateTimeOffset.UtcNow, null, null));

        public ValueTask<DistributedCircuitSnapshot> ControlAsync(string key, DistributedCircuitConfiguration configuration,
            long expectedEpoch, bool isolate, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new DistributedCircuitSnapshot(isolate ? ResilienceCircuitState.Isolated : ResilienceCircuitState.Closed,
                expectedEpoch + 1, configuration.Version, DateTimeOffset.UtcNow, null, null));
    }

    private sealed class SuccessfulReplayHandler : IReplayCommandHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));

        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
    }

    private sealed class SuccessfulSagaHandler : ISagaStepHandler
    {
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));

        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));

        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
    }
}
