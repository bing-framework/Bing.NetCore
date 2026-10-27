using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using BrokenCircuitException = Bing.Resilience.ResilienceCircuitOpenException;
using RateLimiterRejectedException = Bing.Resilience.ResilienceRejectedException;
using Xunit;

namespace Bing.Http.Resilience.Tests;

public sealed class BingHttpConcurrencyAndCircuitTests
{
    [Fact]
    public async Task Reject_over_limit_request_and_reclaim_permit_after_first_request_finishes()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.TrySetResult(true);
                return await releaseFirst.Task.WaitAsync(cancellationToken);
            }
            return ResilienceTestResponses.Create();
        });
        using var host = ResilienceTestHost.Create("concurrency-limit", handler, options =>
        {
            options.Concurrency.Enabled = true;
            options.Concurrency.PermitLimit = 1;
            options.Concurrency.QueueLimit = 0;
        });
        using var firstRequest = ResilienceTestResponses.Get("first");
        var firstSend = host.Client.SendAsync(firstRequest);

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var rejectedRequest = ResilienceTestResponses.Get("rejected");
        await Assert.ThrowsAsync<RateLimiterRejectedException>(() => host.Client.SendAsync(rejectedRequest));

        releaseFirst.TrySetResult(ResilienceTestResponses.Create());
        using var firstResponse = await firstSend.WaitAsync(TimeSpan.FromSeconds(2));
        using var recoveredResponse = await host.Client.SendAsync(ResilienceTestResponses.Get("recovered"));

        Assert.Equal(HttpStatusCode.OK, recoveredResponse.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Keep_circuit_breakers_isolated_between_named_clients()
    {
        var failingCalls = 0;
        var healthyCalls = 0;
        using var failingHandler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref failingCalls);
            return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
        });
        using var healthyHandler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref healthyCalls);
            return Task.FromResult(ResilienceTestResponses.Create());
        });
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        AddCircuitClient(services, "failing-client", "shared-profile", failingHandler);
        AddCircuitClient(services, "healthy-client", "shared-profile", healthyHandler);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var failingClient = factory.CreateClient("failing-client");
        using var healthyClient = factory.CreateClient("healthy-client");

        using var firstFailure = await failingClient.GetAsync("https://service.test/first");
        using var secondFailure = await failingClient.GetAsync("https://service.test/second");
        await Assert.ThrowsAsync<BrokenCircuitException>(() => failingClient.GetAsync("https://service.test/open"));
        using var healthyResponse = await healthyClient.GetAsync("https://service.test/healthy");

        Assert.Equal(2, failingCalls);
        Assert.Equal(1, healthyCalls);
        Assert.Equal(HttpStatusCode.OK, healthyResponse.StatusCode);
    }

    [Fact]
    public async Task Keep_circuit_breakers_isolated_when_client_and_profile_names_collide_after_concatenation()
    {
        var failingCalls = 0;
        var healthyCalls = 0;
        using var failingHandler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref failingCalls);
            return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
        });
        using var healthyHandler = new DelegateHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref healthyCalls);
            return Task.FromResult(ResilienceTestResponses.Create());
        });
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        AddCircuitClient(services, "orders-read", "json", failingHandler);
        AddCircuitClient(services, "orders", "read-json", healthyHandler);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var failingClient = factory.CreateClient("orders-read");
        using var healthyClient = factory.CreateClient("orders");

        using var firstFailure = await failingClient.GetAsync("https://service.test/first");
        using var secondFailure = await failingClient.GetAsync("https://service.test/second");
        await Assert.ThrowsAsync<BrokenCircuitException>(() => failingClient.GetAsync("https://service.test/open"));
        using var healthyResponse = await healthyClient.GetAsync("https://service.test/healthy");

        Assert.Equal(2, failingCalls);
        Assert.Equal(1, healthyCalls);
        Assert.Equal(HttpStatusCode.OK, healthyResponse.StatusCode);
    }

    [Fact]
    public async Task Allow_half_open_probe_to_close_circuit_after_break_duration()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            var attempt = Interlocked.Increment(ref calls);
            var status = attempt <= 2 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(ResilienceTestResponses.Create(status));
        });
        using var host = ResilienceTestHost.Create("half-open-recovery", handler, ConfigureBreaker);

        using var firstFailure = await host.Client.GetAsync("https://service.test/first");
        using var secondFailure = await host.Client.GetAsync("https://service.test/second");
        await Assert.ThrowsAsync<BrokenCircuitException>(() => host.Client.GetAsync("https://service.test/open"));

        await Task.Delay(TimeSpan.FromMilliseconds(650));

        using var halfOpenResponse = await host.Client.GetAsync("https://service.test/probe");
        using var closedResponse = await host.Client.GetAsync("https://service.test/after-probe");

        Assert.Equal(HttpStatusCode.OK, halfOpenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task Preserve_open_circuit_state_when_http_handler_is_recycled()
    {
        var handlerCreations = 0;
        var sends = 0;
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddHttpClient("recycled-client")
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                Interlocked.Increment(ref handlerCreations);
                return new DelegateHttpMessageHandler((_, _) =>
                {
                    Interlocked.Increment(ref sends);
                    return Task.FromResult(ResilienceTestResponses.Create(HttpStatusCode.ServiceUnavailable));
                });
            })
            .SetHandlerLifetime(TimeSpan.FromSeconds(1))
            .AddBingHttpResilience("recycled-profile", options =>
            {
                ConfigureBreaker(options);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        using (var initialClient = factory.CreateClient("recycled-client"))
        {
            using var firstFailure = await initialClient.GetAsync("https://service.test/first");
            using var secondFailure = await initialClient.GetAsync("https://service.test/second");
            await Assert.ThrowsAsync<BrokenCircuitException>(() => initialClient.GetAsync("https://service.test/open"));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        using var recycledClient = factory.CreateClient("recycled-client");

        await Assert.ThrowsAsync<BrokenCircuitException>(() => recycledClient.GetAsync("https://service.test/recycled-open"));

        Assert.Equal(2, handlerCreations);
        Assert.Equal(2, sends);
    }

    private static void AddCircuitClient(
        IServiceCollection services,
        string clientName,
        string profileName,
        HttpMessageHandler handler)
    {
        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddBingHttpResilience(profileName, ConfigureBreaker);
    }

    private static void ConfigureBreaker(BingHttpResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 0;
        options.CircuitBreaker.Enabled = true;
        options.CircuitBreaker.FailureRatio = .5;
        options.CircuitBreaker.MinimumThroughput = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(500);
        options.Concurrency.Enabled = false;
    }
}
