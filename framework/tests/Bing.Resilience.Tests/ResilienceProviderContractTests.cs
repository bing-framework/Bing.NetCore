using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Resilience.Tests;

public sealed class ResilienceProviderContractTests
{
    private const string PipelineName = "contract";

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task GenericVoidAndNullResultsArePreserved(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<string>("nullable", _ => { });
        services.AddBingResiliencePipeline("void", _ => { });
        using var provider = services.BuildServiceProvider();
        var pipelines = provider.GetRequiredService<IResiliencePipelineProvider>();

        var nullResult = await pipelines.GetPipeline<string>("nullable")
            .ExecuteAsync(_ => ValueTask.FromResult<string>(null!));
        var calls = 0;
        await pipelines.GetPipeline("void").ExecuteAsync(_ =>
        {
            calls++;
            return ValueTask.CompletedTask;
        });

        Assert.Null(nullResult);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task RetryRunsConfiguredMaximumAttemptsAndReturnsFinalResult(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
            options.Retry.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName);
        var calls = 0;

        var result = await pipeline.ExecuteAsync(_ =>
        {
            calls++;
            return calls < 3
                ? ValueTask.FromException<int>(new TransientFailure())
                : ValueTask.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task RetryIsDisabledByDefaultAndDoesNotRetryUnselectedErrors(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>("default", _ => { });
        services.AddBingResiliencePipeline<int>("selected", options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var pipelines = provider.GetRequiredService<IResiliencePipelineProvider>();
        var defaultCalls = 0;
        await Assert.ThrowsAsync<TransientFailure>(() => pipelines.GetPipeline<int>("default").ExecuteAsync(_ =>
        {
            defaultCalls++;
            return ValueTask.FromException<int>(new TransientFailure());
        }).AsTask());
        var permanentCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => pipelines.GetPipeline<int>("selected").ExecuteAsync(_ =>
        {
            permanentCalls++;
            return ValueTask.FromException<int>(new InvalidOperationException("permanent"));
        }).AsTask());

        Assert.Equal(1, defaultCalls);
        Assert.Equal(1, permanentCalls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CallerCancellationIsNotRetried(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = _ => true;
        });
        using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
                .ExecuteAsync(_ =>
                {
                    calls++;
                    cancellation.Cancel();
                    return ValueTask.FromCanceled<int>(cancellation.Token);
                }, cancellation.Token).AsTask());

        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task AttemptTimeoutIsNormalizedAndCanBeRetried(string providerName)
    {
        var services = CreateServices(providerName);
        var observed = new List<Exception?>();
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.AttemptTimeout = TimeSpan.FromMilliseconds(40);
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = args =>
            {
                observed.Add(args.Outcome.Exception);
                return args.Outcome.Exception is ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Attempt };
            };
        });
        using var provider = services.BuildServiceProvider();
        var calls = 0;

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
            .ExecuteAsync(async token =>
            {
                calls++;
                if (calls == 1) await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 7;
            });

        Assert.Equal(7, result);
        Assert.Equal(2, calls);
        Assert.Contains(observed, error => error is ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Attempt });
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task ExecutionTimeoutDoesNotRetryOrInvokeFallback(string providerName)
    {
        var services = CreateServices(providerName);
        var calls = 0;
        var fallbacks = 0;
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.ExecutionTimeout = TimeSpan.FromMilliseconds(80);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = _ => true;
            options.Fallback.Enabled = true;
            options.Fallback.ShouldHandle = _ => true;
            options.Fallback.Action = _ =>
            {
                fallbacks++;
                return ValueTask.FromResult(-1);
            };
        });
        using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<ResilienceTimeoutException>(() =>
            provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
                .ExecuteAsync(async token =>
                {
                    calls++;
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return 0;
                }).AsTask());

        Assert.Equal(1, calls);
        Assert.Equal(0, fallbacks);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task FallbackRunsOnlyForSelectedBusinessFailure(string providerName)
    {
        var services = CreateServices(providerName);
        var fallbacks = 0;
        services.AddBingResiliencePipeline<string>(PipelineName, options =>
        {
            options.Fallback.Enabled = true;
            options.Fallback.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
            options.Fallback.Action = args =>
            {
                fallbacks++;
                Assert.IsType<TransientFailure>(args.Outcome.Exception);
                return ValueTask.FromResult("fallback");
            };
        });
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<string>(PipelineName)
            .ExecuteAsync(_ => ValueTask.FromException<string>(new TransientFailure()));

        Assert.Equal("fallback", result);
        Assert.Equal(1, fallbacks);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CircuitBreakerOpensAndAllowsHalfOpenProbe(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options => ConfigureCircuit(options.CircuitBreaker));
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName);
        var fail = true;
        ValueTask<int> Action(CancellationToken _) => fail
            ? ValueTask.FromException<int>(new TransientFailure())
            : ValueTask.FromResult(10);

        await Assert.ThrowsAsync<TransientFailure>(() => pipeline.ExecuteAsync(Action).AsTask());
        await Assert.ThrowsAsync<TransientFailure>(() => pipeline.ExecuteAsync(Action).AsTask());
        await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() => pipeline.ExecuteAsync(Action).AsTask());
        await Task.Delay(650);
        fail = false;
        Assert.Equal(10, await pipeline.ExecuteAsync(Action));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task ManualCircuitIsolationCanBeObservedAndClosed(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            ConfigureCircuit(options.CircuitBreaker);
            options.CircuitBreaker.EnableManualControl = true;
        });
        using var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IResiliencePipelineProvider>();
        var controller = provider.GetRequiredService<IResilienceCircuitController>();
        var identity = new ResiliencePipelineIdentity("generic", PipelineName, typeof(int));
        _ = pipelineProvider.GetPipeline<int>(PipelineName);

        await controller.IsolateAsync(identity);
        Assert.Equal(ResilienceCircuitState.Isolated, controller.GetState(identity));
        await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() =>
            pipelineProvider.GetPipeline<int>(PipelineName).ExecuteAsync(_ => ValueTask.FromResult(1)).AsTask());
        await controller.CloseAsync(identity);

        Assert.Equal(ResilienceCircuitState.Closed, controller.GetState(identity));
        Assert.Equal(1, await pipelineProvider.GetPipeline<int>(PipelineName).ExecuteAsync(_ => ValueTask.FromResult(1)));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task ConcurrencyRejectionDoesNotLeakPermit(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Concurrency.Enabled = true;
            options.Concurrency.PermitLimit = 1;
            options.Concurrency.QueueLimit = 0;
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = pipeline.ExecuteAsync(async _ =>
        {
            started.TrySetResult();
            await release.Task;
            return 1;
        }).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<ResilienceRejectedException>(() => pipeline.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());
        release.TrySetResult();
        Assert.Equal(1, await first);
        Assert.Equal(3, await pipeline.ExecuteAsync(_ => ValueTask.FromResult(3)));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task TokenBucketRejectsAndReplenishes(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.RateLimit.Enabled = true;
            options.RateLimit.TokenLimit = 1;
            options.RateLimit.TokensPerPeriod = 1;
            options.RateLimit.ReplenishmentPeriod = TimeSpan.FromMilliseconds(150);
            options.RateLimit.QueueLimit = 0;
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName);

        Assert.Equal(1, await pipeline.ExecuteAsync(_ => ValueTask.FromResult(1)));
        await Assert.ThrowsAsync<ResilienceRejectedException>(() => pipeline.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());
        await Task.Delay(300);
        Assert.Equal(3, await pipeline.ExecuteAsync(_ => ValueTask.FromResult(3)));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HedgingReturnsFastSecondaryAndCancelsLosingBranch(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<string>(PipelineName, options =>
        {
            options.Hedging.Enabled = true;
            options.Hedging.AllowParallelExecution = true;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(30);
            options.Hedging.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var hedges = 0;
        var losingBranchCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contextValue = new ResiliencePropertyKey<string>("contract-value");
        var context = new ResilienceExecutionContext { OperationName = "hedging-test" };
        context.Set(contextValue, "shared-value");

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<string>(PipelineName)
            .ExecuteAsync(async (executionContext, token) =>
            {
                Assert.True(executionContext.TryGet(contextValue, out var value));
                Assert.Equal("shared-value", value);
                if (Interlocked.Increment(ref hedges) == 1)
                {
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                    catch (OperationCanceledException) { losingBranchCancelled.TrySetResult(); throw; }
                    return "primary";
                }
                return "secondary";
            }, context);

        await losingBranchCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("secondary", result);
        Assert.Equal(2, hedges);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HedgingReturnsPrimaryOutcomeWhenAllBranchesFail(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Hedging.Enabled = true;
            options.Hedging.AllowParallelExecution = true;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(10);
            options.Hedging.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var calls = 0;
        var primaryError = new TransientFailure();
        var secondaryError = new TransientFailure();

        var error = await Assert.ThrowsAsync<TransientFailure>(() =>
            provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
                .ExecuteAsync(_ =>
                {
                    var attempt = Interlocked.Increment(ref calls);
                    return ValueTask.FromException<int>(attempt == 1 ? primaryError : secondaryError);
                }).AsTask());

        Assert.Same(primaryError, error);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HedgingDiscardsLosingResultOnceAndLeavesWinnerCallerOwned(string providerName)
    {
        var services = CreateServices(providerName);
        var discarded = new List<DisposableResult>();
        var loserCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loserCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loser = new DisposableResult(retry: true);
        var winner = new DisposableResult(retry: false);
        services.AddBingResiliencePipeline<DisposableResult>(PipelineName, options =>
        {
            options.Hedging.Enabled = true;
            options.Hedging.AllowParallelExecution = true;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(20);
            options.Hedging.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
            options.DiscardResultAsync = result =>
            {
                discarded.Add(result);
                result.Dispose();
                return ValueTask.CompletedTask;
            };
        });
        using var provider = services.BuildServiceProvider();
        var branches = 0;

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<DisposableResult>(PipelineName)
            .ExecuteAsync(async token =>
            {
                if (Interlocked.Increment(ref branches) == 1)
                {
                    using var registration = token.Register(loserCanceled.SetResult);
                    try
                    {
                        await Task.Delay(120);
                        return loser;
                    }
                    finally { loserCompleted.TrySetResult(); }
                }
                return winner;
            });

        await loserCanceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await loserCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Same(winner, result);
        Assert.Same(loser, Assert.Single(discarded));
        Assert.Equal(1, loser.DisposeCount);
        Assert.Equal(0, winner.DisposeCount);
        winner.Dispose();
        Assert.Equal(1, winner.DisposeCount);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HedgingForksContextAndAttemptNumbers(string providerName)
    {
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Hedging.Enabled = true;
            options.Hedging.AllowParallelExecution = true;
            options.Hedging.MaxHedgedAttempts = 1;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(10);
            options.Hedging.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        });
        using var provider = services.BuildServiceProvider();
        var property = new ResiliencePropertyKey<string>("value");
        var context = new ResilienceExecutionContext();
        context.Set(property, "copied");
        var attempts = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
            .ExecuteAsync((executionContext, _) =>
            {
                Assert.True(executionContext.TryGet(property, out var value));
                attempts[executionContext.AttemptNumber] = value;
                return executionContext.AttemptNumber == 0
                    ? ValueTask.FromException<int>(new TransientFailure())
                    : ValueTask.FromResult(9);
            }, context);

        Assert.Equal(9, result);
        Assert.Contains(0, attempts.Keys);
        Assert.Contains(1, attempts.Keys);
        Assert.All(attempts.Values, value => Assert.Equal("copied", value));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task RetryDiscardsIntermediateResultOnceAndCallerOwnsFinalResult(string providerName)
    {
        var services = CreateServices(providerName);
        var discarded = new List<DisposableResult>();
        services.AddBingResiliencePipeline<DisposableResult>(PipelineName, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = args => args.Outcome.HasResult && args.Outcome.Result!.Retry;
            options.DiscardResultAsync = result =>
            {
                discarded.Add(result);
                result.Dispose();
                return ValueTask.CompletedTask;
            };
        });
        using var provider = services.BuildServiceProvider();
        var calls = 0;

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<DisposableResult>(PipelineName)
            .ExecuteAsync(_ => ValueTask.FromResult(new DisposableResult(++calls < 2)));

        var first = Assert.Single(discarded);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, result.DisposeCount);
        result.Dispose();
        Assert.Equal(1, result.DisposeCount);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task ConcurrencyPermitRemainsHeldWhileRetryResultIsBeingDiscarded(string providerName)
    {
        var services = CreateServices(providerName);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Concurrency.Enabled = true;
            options.Concurrency.PermitLimit = 1;
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = args => args.Outcome.HasResult && args.Outcome.Result == 1;
            options.DiscardResultAsync = async _ =>
            {
                cleanupStarted.TrySetResult();
                await releaseCleanup.Task;
            };
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName);
        var calls = 0;
        var first = pipeline.ExecuteAsync(_ => ValueTask.FromResult(++calls == 1 ? 1 : 2)).AsTask();
        await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<ResilienceRejectedException>(() => pipeline.ExecuteAsync(_ => ValueTask.FromResult(3)).AsTask());
        releaseCleanup.TrySetResult();

        Assert.Equal(2, await first);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CleanupFailureFromRetryIsSurfacedAndPreventsNextAttempt(string providerName)
    {
        var services = CreateServices(providerName);
        var intermediate = new DisposableResult(retry: true);
        var delivered = new DisposableResult(retry: false);
        services.AddBingResiliencePipeline<DisposableResult>(PipelineName, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = args => args.Outcome.HasResult && args.Outcome.Result!.Retry;
            options.DiscardResultAsync = result =>
            {
                result.Dispose();
                if (result.Retry) throw new InvalidOperationException("cleanup failed");
                return ValueTask.CompletedTask;
            };
        });
        using var provider = services.BuildServiceProvider();
        var calls = 0;

        var error = await Assert.ThrowsAsync<ResilienceResultCleanupException>(() =>
            provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<DisposableResult>(PipelineName)
                .ExecuteAsync(_ => ValueTask.FromResult(calls++ == 0 ? intermediate : delivered)).AsTask());

        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal("cleanup failed", error.InnerException!.Message);
        Assert.Equal(1, calls);
        Assert.Equal(1, intermediate.DisposeCount);
        Assert.Equal(0, delivered.DisposeCount);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public void RegistrationRejectsDuplicateProviderPipelineAndUnknownPipeline(string providerName)
    {
        var services = new ServiceCollection();
        var builder = RegisterProvider(services, providerName);
        Assert.Throws<ResilienceConfigurationException>(() =>
            RegisterProvider(services, providerName));
        builder.UseDefaultProvider(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, _ => { });
        Assert.Throws<ResilienceConfigurationException>(() => services.AddBingResiliencePipeline<int>(PipelineName, _ => { }));
        using var provider = services.BuildServiceProvider();
        Assert.Throws<ResilienceConfigurationException>(() =>
            provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("missing"));
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public void RegistrationRejectsMissingCapabilitiesAndUnknownProvider(string providerName)
    {
        var missingCapabilities = new ServiceCollection();
        missingCapabilities.AddBingResilience()
            .AddProvider(new RegistrationOnlyProvider("limited", ResilienceCapabilities.None))
            .UseDefaultProvider("limited");
        missingCapabilities.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.ShouldHandle = _ => true;
        });
        using (var provider = missingCapabilities.BuildServiceProvider())
            Assert.Throws<ResilienceConfigurationException>(() => provider.GetRequiredService<IResiliencePipelineProvider>());

        var unknown = new ServiceCollection();
        var builder = RegisterProvider(unknown, providerName);
        builder.UseDefaultProvider("absent");
        unknown.AddBingResiliencePipeline<int>(PipelineName, _ => { });
        using var unknownProvider = unknown.BuildServiceProvider();
        Assert.Throws<ResilienceConfigurationException>(() => unknownProvider.GetRequiredService<IResiliencePipelineProvider>());
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task RegistrationFreezesOptionsAtRegistrationTime(string providerName)
    {
        ResiliencePipelineOptions<int>? registered = null;
        var options = new ResiliencePipelineOptions<int>();
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.Zero;
        options.Retry.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
        var services = CreateServices(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, configured =>
        {
            registered = configured;
            configured.Retry.MaxRetryAttempts = options.Retry.MaxRetryAttempts;
            configured.Retry.Delay = options.Retry.Delay;
            configured.Retry.ShouldHandle = options.Retry.ShouldHandle;
        });
        Assert.NotNull(registered);
        registered.Retry.MaxRetryAttempts = 0;
        registered.Retry.ShouldHandle = _ => false;
        using var provider = services.BuildServiceProvider();
        var calls = 0;

        var result = await provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>(PipelineName)
            .ExecuteAsync(_ => ++calls == 1
                ? ValueTask.FromException<int>(new TransientFailure())
                : ValueTask.FromResult(5));

        Assert.Equal(5, result);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HostStartValidatesAndHostDisposalReleasesRegistry(string providerName)
    {
        var builder = Host.CreateApplicationBuilder();
        var services = builder.Services;
        var resilience = RegisterProvider(services, providerName);
        resilience.UseDefaultProvider(providerName);
        services.AddBingResiliencePipeline<int>(PipelineName, options =>
        {
            options.RateLimit.Enabled = true;
            options.RateLimit.TokenLimit = 1;
            options.RateLimit.TokensPerPeriod = 1;
            options.RateLimit.ReplenishmentPeriod = TimeSpan.FromHours(1);
        });
        using var host = builder.Build();
        await host.StartAsync();
        var pipelines = host.Services.GetRequiredService<IResiliencePipelineProvider>();
        _ = pipelines.GetPipeline<int>(PipelineName);
        await host.StopAsync();
        host.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pipelines.GetPipeline<int>(PipelineName));
    }

    private static ServiceCollection CreateServices(string providerName)
    {
        var services = new ServiceCollection();
        var builder = RegisterProvider(services, providerName);
        builder.UseDefaultProvider(providerName);
        return services;
    }

    private static BingResilienceBuilder RegisterProvider(IServiceCollection services, string providerName)
    {
        var builder = services.AddBingResilience();
        return providerName switch
        {
            "Fences" => builder.AddFencesProvider(providerName),
            "Polly" => builder.AddPollyProvider(providerName),
            "BuiltIn" => builder.AddBuiltInProvider(providerName),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Unknown test provider.")
        };
    }

    private static void ConfigureCircuit(ResilienceCircuitBreakerOptions<int> options)
    {
        options.Enabled = true;
        options.FailureRatio = 0.5;
        options.MinimumThroughput = 2;
        options.SamplingDuration = TimeSpan.FromSeconds(3);
        options.BreakDuration = TimeSpan.FromMilliseconds(500);
        options.ShouldHandle = args => args.Outcome.Exception is TransientFailure;
    }

    private sealed class TransientFailure : Exception;

    private sealed class DisposableResult(bool retry) : IDisposable
    {
        public bool Retry { get; } = retry;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class RegistrationOnlyProvider(string name, ResilienceCapabilities capabilities) : IResilienceProvider
    {
        public string Name { get; } = name;
        public ResilienceCapabilities Capabilities { get; } = capabilities;
        public IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context) =>
            throw new NotSupportedException("此 Provider 仅用于启动能力校验。");
    }
}
