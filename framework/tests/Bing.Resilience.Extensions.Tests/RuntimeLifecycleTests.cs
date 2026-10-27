using System.Collections.Concurrent;
using System.Net;
using Bing.Http.Resilience;
using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class RuntimeLifecycleTests
{
    [Fact]
    public async Task HttpContentKeepsRetiredGenerationUntilContentIsDisposed()
    {
        var services = HttpServices("content-lease");
        using var host = services.BuildServiceProvider();
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("content-lease");
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();

        using var response = await client.GetAsync("http://service.test/content", HttpCompletionOption.ResponseHeadersRead);
        var switched = await runtime.SwitchAsync("http:content-lease", "fences", 0);

        Assert.False(switched.PreviousGenerationDrained);
        Assert.Equal(1, switched.Snapshot.RetiredGenerations);

        response.Content.Dispose();
        await WaitForRetiredGenerationsAsync(runtime, "http:content-lease", 0);
    }

    [Fact]
    public async Task HttpStreamDisposalReleasesItsGenerationLease()
    {
        var services = HttpServices("stream-lease");
        using var host = services.BuildServiceProvider();
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("stream-lease");
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();

        using var response = await client.GetAsync("http://service.test/stream", HttpCompletionOption.ResponseHeadersRead);
        using var stream = await response.Content.ReadAsStreamAsync();
        var switched = await runtime.SwitchAsync("http:stream-lease", "fences", 0);

        Assert.False(switched.PreviousGenerationDrained);
        Assert.Equal(1, switched.Snapshot.RetiredGenerations);

        stream.Dispose();
        await WaitForRetiredGenerationsAsync(runtime, "http:stream-lease", 0);
    }

    [Fact]
    public async Task PartiallyBuiltCandidateIsDisposedAndActivePipelineRemainsUsable()
    {
        var candidate = new PartialBuildProvider();
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddProvider(candidate)
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching();
        services.AddBingResiliencePipeline<int>("first", options => options.SwitchGroup = "partial");
        services.AddBingResiliencePipeline<int>("second", options => options.SwitchGroup = "partial");

        using var host = services.BuildServiceProvider();
        var pipelines = host.GetRequiredService<IResiliencePipelineProvider>();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var first = pipelines.GetPipeline<int>("first");

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.SwitchAsync("partial", "partial", 0).AsTask());

        Assert.Equal(1, candidate.Resource.DisposeCount);
        Assert.Equal(0, runtime.GetSnapshot("partial").Version);
        Assert.Null(runtime.GetSnapshot("partial").ProviderName);
        Assert.Equal(23, await first.ExecuteAsync(_ => ValueTask.FromResult(23)));
    }

    [Fact]
    public async Task ConcurrentSwitchesWithSameExpectedVersionPublishOnlyOneCandidate()
    {
        var candidate = new ConcurrentBuildProvider();
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddProvider(candidate)
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching();
        services.AddBingResiliencePipeline<int>("work", options => options.SwitchGroup = "cas");

        var host = services.BuildServiceProvider();
        try
        {
            var pipelines = host.GetRequiredService<IResiliencePipelineProvider>();
            var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
            var handle = pipelines.GetPipeline<int>("work");

            var first = Task.Factory.StartNew(
                () => runtime.SwitchAsync("cas", "candidate", 0).AsTask(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            var second = Task.Factory.StartNew(
                () => runtime.SwitchAsync("cas", "candidate", 0).AsTask(),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

            Assert.True(await Task.Run(() => candidate.BothBuildsEntered.Wait(TimeSpan.FromSeconds(5))));
            candidate.ReleaseBuilds.Set();

            var outcomes = await Task.WhenAll(CaptureSwitchAsync(first), CaptureSwitchAsync(second));
            Assert.Single(outcomes, outcome => outcome.Succeeded);
            Assert.Single(outcomes, outcome => outcome.Error is ResilienceConfigurationException);
            Assert.Equal(1, runtime.GetSnapshot("cas").Version);
            Assert.Equal("candidate", runtime.GetSnapshot("cas").ProviderName);
            Assert.Equal(1, candidate.Resources.Count(resource => resource.DisposeCount == 1));
            Assert.Equal(17, await handle.ExecuteAsync(_ => ValueTask.FromResult(17)));
        }
        finally
        {
            candidate.ReleaseBuilds.Set();
            host.Dispose();
        }

        Assert.Equal(2, candidate.Resources.Count);
        Assert.All(candidate.Resources, resource => Assert.Equal(1, resource.DisposeCount));
    }

    [Fact]
    public async Task OpenCircuitSurvivesProviderSwitchAndAllowsOnlyOneHalfOpenProbe()
    {
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddFencesProvider("fences")
            .AddPollyProvider("polly")
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching();
        services.AddBingResiliencePipeline<int>("breaker", options =>
        {
            options.SwitchGroup = "circuit";
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.EnableManualControl = true;
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(500);
            options.CircuitBreaker.ShouldHandle = args => args.Outcome.Exception is IOException;
        });

        using var host = services.BuildServiceProvider();
        var pipelines = host.GetRequiredService<IResiliencePipelineProvider>();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var controller = host.GetRequiredService<IResilienceCircuitController>();
        var pipeline = pipelines.GetPipeline<int>("breaker");
        var identity = new ResiliencePipelineIdentity("generic", "breaker", typeof(int));

        for (var index = 0; index < 2; index++)
            await Assert.ThrowsAsync<IOException>(() => pipeline.ExecuteAsync(
                _ => ValueTask.FromException<int>(new IOException("transient"))).AsTask());
        Assert.Equal(ResilienceCircuitState.Open, controller.GetState(identity));

        await runtime.SwitchAsync("circuit", "fences", 0);
        Assert.Equal(ResilienceCircuitState.Open, controller.GetState(identity));
        await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() => pipeline.ExecuteAsync(
            _ => ValueTask.FromResult(1)).AsTask());

        await Task.Delay(TimeSpan.FromMilliseconds(650));
        var probeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeCalls = 0;
        var probe = pipeline.ExecuteAsync(async _ =>
        {
            Interlocked.Increment(ref probeCalls);
            probeEntered.TrySetResult();
            await releaseProbe.Task;
            return 7;
        }).AsTask();

        try
        {
            await probeEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(ResilienceCircuitState.HalfOpen, controller.GetState(identity));
            await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() => pipeline.ExecuteAsync(
                _ => ValueTask.FromResult(8)).AsTask());
            Assert.Equal(1, Volatile.Read(ref probeCalls));
        }
        finally
        {
            releaseProbe.TrySetResult();
        }

        Assert.Equal(7, await probe);
        Assert.Equal(ResilienceCircuitState.Closed, controller.GetState(identity));
    }

    private static ServiceCollection HttpServices(string clientName)
    {
        var services = new ServiceCollection();
        services.AddBingResilience()
            .AddBuiltInProvider("builtin")
            .AddFencesProvider("fences")
            .UseDefaultProvider("builtin")
            .AddRuntimeSwitching(options => options.DrainTimeout = TimeSpan.FromMilliseconds(30));
        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => new FixedResponseHandler())
            .AddBingHttpResilience($"profile-{clientName}", options =>
            {
                options.Retry.MaxRetryAttempts = 0;
                options.CircuitBreaker.Enabled = false;
                options.Concurrency.Enabled = false;
            });
        return services;
    }

    private static async Task WaitForRetiredGenerationsAsync(IResilienceRuntimeManager runtime, string group, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (runtime.GetSnapshot(group).RetiredGenerations != count)
            await Task.Delay(10, timeout.Token);
    }

    private static async Task<SwitchOutcome> CaptureSwitchAsync(Task<ResilienceSwitchResult> task)
    {
        try
        {
            return new(true, null, await task);
        }
        catch (Exception error)
        {
            return new(false, error, null);
        }
    }

    private sealed record SwitchOutcome(bool Succeeded, Exception? Error, ResilienceSwitchResult? Result);

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            });
    }

    private sealed class PartialBuildProvider : IResilienceRuntimeAwareProvider
    {
        private int _created;
        public string Name => "partial";
        public ResilienceCapabilities Capabilities => ResilienceCapabilities.All;
        public TrackingResource Resource { get; } = new();

        public IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context)
        {
            var number = Interlocked.Increment(ref _created);
            if (number == 2) throw new InvalidOperationException("candidate build failed after one pipeline");
            context.RegisterResource(Resource);
            return new PassThroughPipeline<T>();
        }
    }

    private sealed class ConcurrentBuildProvider : IResilienceRuntimeAwareProvider
    {
        private int _created;
        public string Name => "candidate";
        public ResilienceCapabilities Capabilities => ResilienceCapabilities.All;
        public ManualResetEventSlim BothBuildsEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseBuilds { get; } = new(false);
        public ConcurrentBag<TrackingResource> Resources { get; } = new();

        public IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context)
        {
            var resource = new TrackingResource();
            Resources.Add(resource);
            context.RegisterResource(resource);
            if (Interlocked.Increment(ref _created) == 2) BothBuildsEntered.Set();
            if (!ReleaseBuilds.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("concurrent candidate builds were not released");
            return new PassThroughPipeline<T>();
        }
    }

    private sealed class TrackingResource : IDisposable
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class PassThroughPipeline<T> : IResiliencePipeline<T>
    {
        public ValueTask<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken = default) =>
            action(cancellationToken);

        public ValueTask<T> ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> action,
            ResilienceExecutionContext context, CancellationToken cancellationToken = default) => action(context, cancellationToken);
    }
}
