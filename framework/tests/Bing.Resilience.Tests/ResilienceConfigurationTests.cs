using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Tests;

public sealed class ResilienceConfigurationTests
{
    [Fact]
    public void RegistrationRejectsInvalidRecoveryCombinationsWithoutResolvingServices()
    {
        var factories = 0;
        var invalid = new Action<ResiliencePipelineOptions<int>>[]
        {
            o => o.Retry.MaxRetryAttempts = 1,
            o => { o.Hedging.Enabled = true; o.Hedging.ShouldHandle = _ => true; },
            o => { o.Retry.MaxRetryAttempts = 1; o.Retry.ShouldHandle = _ => true;
                o.Hedging.Enabled = true; o.Hedging.AllowParallelExecution = true; o.Hedging.ShouldHandle = _ => true; },
            o => { o.Fallback.Enabled = true; o.Fallback.ShouldHandle = _ => true; },
            o => o.CircuitBreaker.EnableManualControl = true,
            o => { o.ExecutionTimeout = TimeSpan.FromSeconds(1); o.AttemptTimeout = TimeSpan.FromSeconds(2); },
            o => o.RateLimit.Enabled = true,
            o => { o.Concurrency.Enabled = true; o.Concurrency.PermitLimit = 0; }
        };
        var services = new ServiceCollection();
        services.AddSingleton(_ => { factories++; return new object(); });
        services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
        foreach (var configure in invalid)
            Assert.Throws<ResilienceConfigurationException>(() => services.AddBingResiliencePipeline<int>("invalid", configure));
        services.AddBingResiliencePipeline<int>("valid", _ => { });
        Assert.Equal(0, factories);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("valid");
        Assert.Equal(0, factories);
        _ = provider.GetRequiredService<object>();
        Assert.Equal(1, factories);
    }

    [Theory]
    [InlineData("Fences")]
    [InlineData("Polly")]
    [InlineData("BuiltIn")]
    public async Task NamesPurposesTypesAndPartitionsRemainDistinct(string providerName)
    {
        var services = new ServiceCollection();
        var builder = services.AddBingResilience();
        switch (providerName)
        {
            case "Fences": builder.AddFencesProvider("engine"); break;
            case "Polly": builder.AddPollyProvider("engine"); break;
            case "BuiltIn": builder.AddBuiltInProvider("engine"); break;
            default: throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Unknown test provider.");
        }
        builder.UseDefaultProvider("engine");
        services.AddBingResiliencePipeline<int>("Read", _ => { });
        services.AddBingResiliencePipeline<int>("read", _ => { });
        services.AddBingResiliencePipeline<string>("Read", _ => { });
        services.AddBingResiliencePipeline<int>("Read", o => o.Purpose = "other");
        services.AddBingResiliencePipeline<int>("Read", o => o.Partition = "secondary");
        using var host = services.BuildServiceProvider();
        var provider = host.GetRequiredService<IResiliencePipelineProvider>();
        var standard = provider.GetPipeline<int>("Read");
        Assert.Same(standard, provider.GetPipeline<int>("Read"));
        Assert.NotSame(standard, provider.GetPipeline<int>("read"));
        Assert.NotSame(standard, provider.GetPipeline<int>("Read", "other"));
        Assert.NotSame(standard, provider.GetPipeline<int>("Read", partition: "secondary"));
        Assert.Equal("dto", await provider.GetPipeline<string>("Read").ExecuteAsync(_ => ValueTask.FromResult("dto")));
        Assert.Throws<ResilienceConfigurationException>(() => provider.GetPipeline<int>("READ"));
    }
}
