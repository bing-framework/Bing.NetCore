using System.Collections.Concurrent;
using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Tests;

public sealed class ResilienceStrategyBoundaryTests
{
    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CircuitBreakerAllowsOnlyOneConcurrentHalfOpenProbe(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>("half-open", options =>
        {
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(3);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(500);
            options.CircuitBreaker.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("half-open");
        var failures = 0;
        for (var index = 0; index < 2; index++)
            await Assert.ThrowsAsync<TransientFailure>(() => pipeline.ExecuteAsync(_ =>
            {
                Interlocked.Increment(ref failures);
                return ValueTask.FromException<int>(new TransientFailure());
            }).AsTask());

        await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() =>
            pipeline.ExecuteAsync(_ => ValueTask.FromResult(0)).AsTask());
        await Task.Delay(700);

        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeCalls = 0;
        var probe = pipeline.ExecuteAsync(async token =>
        {
            Interlocked.Increment(ref probeCalls);
            probeStarted.TrySetResult();
            await releaseProbe.Task.WaitAsync(token);
            return 7;
        }).AsTask();
        try
        {
            await probeStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() =>
                pipeline.ExecuteAsync(_ =>
                {
                    Interlocked.Increment(ref probeCalls);
                    return ValueTask.FromResult(8);
                }).AsTask());
            Assert.Equal(1, Volatile.Read(ref probeCalls));
        }
        finally { releaseProbe.TrySetResult(true); }

        Assert.Equal(7, await probe.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CallerCancellationCancelsAllHedgesAndDiscardsLateResultsOnce(string providerName)
    {
        var services = CreateServices(providerName);
        var results = new ConcurrentBag<TrackingResult>();
        var cancelledBranches = new ConcurrentBag<int>();
        var disposedCount = 0;
        var allDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondaryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddBingResiliencePipeline<TrackingResult>("caller-cancel", options =>
        {
            options.Hedging.Enabled = true;
            options.Hedging.AllowParallelExecution = true;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(20);
            options.Hedging.ShouldHandle = _ => true;
            options.DiscardResultAsync = result =>
            {
                result.Dispose();
                if (Interlocked.Increment(ref disposedCount) == 2) allDisposed.TrySetResult();
                return ValueTask.CompletedTask;
            };
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<TrackingResult>("caller-cancel");
        using var cancellation = new CancellationTokenSource();
        var execute = pipeline.ExecuteAsync(async (context, token) =>
        {
            if (context.AttemptNumber == 0) primaryStarted.TrySetResult();
            else secondaryStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException)
            {
                cancelledBranches.Add(context.AttemptNumber);
                var result = new TrackingResult();
                results.Add(result);
                return result;
            }
            throw new InvalidOperationException("The branch should have been cancelled.");
        }, new ResilienceExecutionContext(), cancellation.Token).AsTask();

        await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await secondaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execute);
        await allDisposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { 0, 1 }, cancelledBranches.OrderBy(branch => branch).ToArray());
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(1, result.DisposeCount));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task FallbackActionIsCancelledByExecutionBudget(string providerName)
    {
        var services = CreateServices(providerName);
        var fallbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fallbackCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddBingResiliencePipeline<int>("fallback-budget", options =>
        {
            options.ExecutionTimeout = TimeSpan.FromMilliseconds(250);
            options.Fallback.Enabled = true;
            options.Fallback.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
            options.Fallback.Action = async args =>
            {
                fallbackStarted.TrySetResult();
                using var registration = args.CancellationToken.Register(() => fallbackCancelled.TrySetResult());
                await Task.Delay(Timeout.InfiniteTimeSpan, args.CancellationToken);
                return -1;
            };
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("fallback-budget");

        var error = await Assert.ThrowsAsync<ResilienceTimeoutException>(() =>
            pipeline.ExecuteAsync(_ => ValueTask.FromException<int>(new TransientFailure())).AsTask());

        Assert.Equal(ResilienceTimeoutKind.Execution, error.Kind);
        await fallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await fallbackCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static ServiceCollection CreateServices(string providerName)
    {
        var services = new ServiceCollection();
        var builder = services.AddBingResilience();
        switch (providerName)
        {
            case "Fences": builder.AddFencesProvider(providerName); break;
            case "Polly": builder.AddPollyProvider(providerName); break;
            case "BuiltIn": builder.AddBuiltInProvider(providerName); break;
            default: throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Unknown test provider.");
        }
        builder.UseDefaultProvider(providerName);
        return services;
    }

    private sealed class TransientFailure : Exception;

    private sealed class TrackingResult : IDisposable
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}
