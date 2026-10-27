using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Tests;

public sealed class ResilienceCleanupTests
{
    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task CleanupFailureCannotBeConvertedIntoFallbackSuccess(string providerName)
    {
        var services = new ServiceCollection();
        var builder = RegisterProvider(services, providerName);
        builder.UseDefaultProvider(providerName);
        var original = new InvalidOperationException("dispose failed");
        var attempts = 0; var fallbacks = 0; var disposals = 0;
        services.AddBingResiliencePipeline<string>("cleanup", options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.ShouldHandle = _ => true;
            options.DiscardResultAsync = _ => { disposals++; throw original; };
            options.Fallback.Enabled = true;
            options.Fallback.ShouldHandle = _ => true;
            options.Fallback.Action = _ => { fallbacks++; return new("hidden-failure"); };
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<string>("cleanup");
        var error = await Assert.ThrowsAsync<ResilienceResultCleanupException>(async () =>
            await pipeline.ExecuteAsync(_ => { attempts++; return new ValueTask<string>("retryable"); }));
        Assert.Same(original, error.InnerException);
        Assert.Equal(1, attempts); Assert.Equal(1, disposals); Assert.Equal(0, fallbacks);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task HedgingHoldsLogicalPermitUntilLosingResultCleanupFinishes(string providerName)
    {
        var services = new ServiceCollection();
        var builder = RegisterProvider(services, providerName);
        builder.UseDefaultProvider(providerName);
        var cleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddBingResiliencePipeline<string>("cleanup", options =>
        {
            options.Concurrency.Enabled = true; options.Concurrency.PermitLimit = 1;
            options.Hedging.Enabled = true; options.Hedging.AllowParallelExecution = true;
            options.Hedging.Delay = TimeSpan.FromMilliseconds(10);
            options.Hedging.ShouldHandle = _ => false;
            options.DiscardResultAsync = async _ => { cleanup.TrySetResult(true); await release.Task; };
        });
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<string>("cleanup");
        var first = pipeline.ExecuteAsync(async (ctx, token) =>
        {
            if (ctx.AttemptNumber == 0)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { return "late"; }
            }
            return "winner";
        }, new()).AsTask();
        try
        {
            await cleanup.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<ResilienceRejectedException>(async () => await pipeline.ExecuteAsync(_ => new ValueTask<string>("other")));
        }
        finally { release.TrySetResult(true); }
        Assert.Equal("winner", await first);
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
}
