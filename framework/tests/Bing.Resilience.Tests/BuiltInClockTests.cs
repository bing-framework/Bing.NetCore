using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Tests;

public sealed class BuiltInClockTests
{
    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task InjectedTimeProviderDrivesTokenRefillAndExecutionTimeout(string providerName)
    {
        var clock = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        var builder = RegisterProvider(services, providerName);
        builder.UseDefaultProvider(providerName);
        services.AddBingResiliencePipeline<int>("rate", options =>
        {
            options.RateLimit.Enabled = true;
            options.RateLimit.TokenLimit = 1;
            options.RateLimit.TokensPerPeriod = 1;
            options.RateLimit.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });
        services.AddBingResiliencePipeline<int>("timeout", options =>
            options.ExecutionTimeout = TimeSpan.FromSeconds(10));
        using var provider = services.BuildServiceProvider();
        var pipelines = provider.GetRequiredService<IResiliencePipelineProvider>();
        var rate = pipelines.GetPipeline<int>("rate");

        Assert.Equal(1, await rate.ExecuteAsync(_ => ValueTask.FromResult(1)));
        await Assert.ThrowsAsync<ResilienceRejectedException>(() =>
            rate.ExecuteAsync(_ => ValueTask.FromResult(2)).AsTask());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, await rate.ExecuteAsync(_ => ValueTask.FromResult(3)));

        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = pipelines.GetPipeline<int>("timeout").ExecuteAsync(async token =>
        {
            callbackStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 0;
        }).AsTask();
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        clock.Advance(TimeSpan.FromSeconds(10));
        var timeout = await Assert.ThrowsAsync<ResilienceTimeoutException>(() =>
            execution.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(ResilienceTimeoutKind.Execution, timeout.Kind);
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
