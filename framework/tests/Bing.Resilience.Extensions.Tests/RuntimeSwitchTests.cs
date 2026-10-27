using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class RuntimeSwitchTests
{
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddBuiltInProvider("builtin").AddFencesProvider("fences").AddPollyProvider("polly")
            .UseDefaultProvider("builtin").AddRuntimeSwitching(o => o.DrainTimeout = TimeSpan.FromMilliseconds(40));
        return services;
    }
    [Fact]
    public async Task CachedHandleUsesNewGenerationWhileInflightNestedCallsStayPinned()
    {
        var services = Services();
        services.AddBingResiliencePipeline<long>("outer", o => o.SwitchGroup = "group");
        services.AddBingResiliencePipeline<long>("inner", o => o.SwitchGroup = "group");
        using var host = services.BuildServiceProvider();
        var pipelines = host.GetRequiredService<IResiliencePipelineProvider>();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var leases = (IResilienceRuntimeLeaseProvider)pipelines;
        var handle = pipelines.GetPipeline<long>("outer");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = handle.ExecuteAsync(async (context, token) =>
        {
            entered.SetResult(); await gate.Task.WaitAsync(token);
            return await pipelines.GetPipeline<long>("inner").ExecuteAsync((nested, _) =>
            {
                using var lease = leases.AcquireLease("group", nested);
                return ValueTask.FromResult(lease.Version);
            }, context, token);
        }, new()).AsTask();
        await entered.Task;
        var switched = await runtime.SwitchAsync("group", "polly", 0);
        Assert.False(switched.PreviousGenerationDrained);
        Assert.Equal(1, switched.Snapshot.Version);
        Assert.Equal(1, await handle.ExecuteAsync((context, _) => { using var lease = leases.AcquireLease("group", context); return ValueTask.FromResult(lease.Version); }, new()));
        gate.SetResult();
        Assert.Equal(0, await running);
        Assert.Equal(0, runtime.GetSnapshot("group").RetiredGenerations);
        Assert.Same(handle, pipelines.GetPipeline<long>("outer"));
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => runtime.SwitchAsync("group", "fences", 0).AsTask());
    }
    [Fact]
    public async Task SwitchDoesNotRefillTokensOrResetManualIsolation()
    {
        var services = Services();
        services.AddBingResiliencePipeline<int>("quota", o =>
        {
            o.RateLimit.Enabled = true; o.RateLimit.TokenLimit = 1; o.RateLimit.TokensPerPeriod = 1; o.RateLimit.ReplenishmentPeriod = TimeSpan.FromHours(1);
        });
        services.AddBingResiliencePipeline<int>("circuit", o =>
        {
            o.CircuitBreaker.Enabled = true; o.CircuitBreaker.EnableManualControl = true; o.CircuitBreaker.ShouldHandle = a => a.Outcome.Exception is IOException;
        });
        using var host = services.BuildServiceProvider();
        var pipes = host.GetRequiredService<IResiliencePipelineProvider>();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var control = host.GetRequiredService<IResilienceCircuitController>();
        var identity = new ResiliencePipelineIdentity("generic", "circuit", typeof(int));
        await pipes.GetPipeline<int>("quota").ExecuteAsync(_ => ValueTask.FromResult(1));
        await control.IsolateAsync(identity);
        foreach (var (engine, version) in new[] { ("fences", 0L), ("polly", 1L), ("builtin", 2L) })
        {
            Assert.True((await runtime.SwitchAsync("generic:quota", engine, version)).PreviousGenerationDrained);
            await runtime.SwitchAsync("generic:circuit", engine, version);
            await Assert.ThrowsAsync<ResilienceRejectedException>(() => pipes.GetPipeline<int>("quota").ExecuteAsync(_ => ValueTask.FromResult(1)).AsTask());
            Assert.Equal(ResilienceCircuitState.Isolated, control.GetState(identity));
            await Assert.ThrowsAsync<ResilienceCircuitOpenException>(() => pipes.GetPipeline<int>("circuit").ExecuteAsync(_ => ValueTask.FromResult(1)).AsTask());
        }
        await control.CloseAsync(identity);
        Assert.Equal(9, await pipes.GetPipeline<int>("circuit").ExecuteAsync(_ => ValueTask.FromResult(9)));
    }
    [Fact]
    public async Task SharedConcurrencyRemainsHeldAcrossProviderSwitch()
    {
        var services = Services();
        services.AddBingResiliencePipeline<int>("limited", o => { o.Concurrency.Enabled = true; o.Concurrency.PermitLimit = 1; });
        using var host = services.BuildServiceProvider();
        var pipe = host.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("limited");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = pipe.ExecuteAsync(async _ => { entered.SetResult(); await release.Task; return 1; }).AsTask();
        await entered.Task;
        await host.GetRequiredService<IResilienceRuntimeManager>().SwitchAsync("generic:limited", "fences", 0);
        await Assert.ThrowsAsync<ResilienceRejectedException>(() => pipe.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());
        release.SetResult(); Assert.Equal(1, await first);
        Assert.Equal(3, await pipe.ExecuteAsync(_ => ValueTask.FromResult(3)));
    }
    [Fact]
    public async Task FailedCandidateDoesNotChangeVersionAndBoundedRetirementRejectsFurtherSwitch()
    {
        var services = Services();
        services.AddBingResiliencePipeline<int>("work", _ => { });
        using var host = services.BuildServiceProvider();
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var pipes = host.GetRequiredService<IResiliencePipelineProvider>();
        using var lease = ((IResilienceRuntimeLeaseProvider)pipes).AcquireLease("generic:work");
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => runtime.SwitchAsync("generic:work", "missing", 0).AsTask());
        Assert.Equal(0, runtime.GetSnapshot("generic:work").Version);
        await runtime.SwitchAsync("generic:work", "fences", 0);
        using var lease2 = ((IResilienceRuntimeLeaseProvider)pipes).AcquireLease("generic:work");
        await runtime.SwitchAsync("generic:work", "polly", 1);
        using var lease3 = ((IResilienceRuntimeLeaseProvider)pipes).AcquireLease("generic:work");
        await runtime.SwitchAsync("generic:work", "builtin", 2);
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => runtime.SwitchAsync("generic:work", "fences", 3).AsTask());
        Assert.Equal(3, runtime.GetSnapshot("generic:work").RetiredGenerations);
    }
}
