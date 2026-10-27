using System.Net;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Discovery;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class WeightedDiscoveryTests
{
    [Fact]
    public void WeightThreeToOneProducesSeventyFiveTwentyFivePrimaryDistribution()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var snapshot = Snapshot("catalog", TimeProvider.System, Endpoint("a", 3), Endpoint("b", 1));

        var selected = Enumerable.Range(0, 100).Select(_ => selector.Select(snapshot)[0].Id).ToArray();

        Assert.Equal(75, selected.Count(id => id == "a"));
        Assert.Equal(25, selected.Count(id => id == "b"));
    }

    [Fact]
    public void EqualWeightsRotateFairly()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var snapshot = Snapshot("catalog", TimeProvider.System, Endpoint("a"), Endpoint("b"));

        var selected = Enumerable.Range(0, 100).Select(_ => selector.Select(snapshot)[0].Id).ToArray();

        Assert.Equal(50, selected.Count(id => id == "a"));
        Assert.Equal(50, selected.Count(id => id == "b"));
    }

    [Fact]
    public void ConcurrentSelectionsPreserveConfiguredWeightRatio()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var snapshot = Snapshot("catalog", TimeProvider.System, Endpoint("a", 3), Endpoint("b", 1));
        var a = 0;
        var b = 0;

        Parallel.For(0, 4000, _ =>
        {
            if (selector.Select(snapshot)[0].Id == "a") Interlocked.Increment(ref a);
            else Interlocked.Increment(ref b);
        });

        Assert.Equal(3000, a);
        Assert.Equal(1000, b);
    }

    [Fact]
    public void ChangingWeightsResetsServiceRotationState()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var clock = TimeProvider.System;
        Assert.Equal("a", selector.Select(Snapshot("catalog", clock, Endpoint("a", 3), Endpoint("b", 1)))[0].Id);

        var changed = Snapshot("catalog", clock, Endpoint("a"), Endpoint("b"));

        Assert.Equal("a", selector.Select(changed)[0].Id);
    }

    [Fact]
    public void ChangingMembershipResetsServiceRotationState()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var clock = TimeProvider.System;
        Assert.Equal("a", selector.Select(Snapshot("catalog", clock, Endpoint("a", 3), Endpoint("b", 1)))[0].Id);

        var changed = Snapshot("catalog", clock, Endpoint("a", 2), Endpoint("c", 1));

        Assert.Equal("a", selector.Select(changed)[0].Id);
    }

    [Fact]
    public void EmptySnapshotReturnsNoCandidatesAndClearsPreviousState()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var clock = TimeProvider.System;
        var populated = Snapshot("catalog", clock, Endpoint("a", 3), Endpoint("b", 1));
        Assert.Equal("a", selector.Select(populated)[0].Id);
        Assert.Equal("a", selector.Select(populated)[0].Id);

        Assert.Empty(selector.Select(Snapshot("catalog", clock)));

        Assert.Equal("a", selector.Select(populated)[0].Id);
    }

    [Fact]
    public void ExpiredRotationStateIsRecreatedAfterRetentionTtl()
    {
        var clock = new FakeTimeProvider();
        var selector = new WeightedRoundRobinServiceEndpointSelector(stateRetention: TimeSpan.FromSeconds(10), timeProvider: clock);
        var snapshot = Snapshot("catalog", clock, Endpoint("a", 3), Endpoint("b", 1));
        Assert.Equal("a", selector.Select(snapshot)[0].Id);
        Assert.Equal("a", selector.Select(snapshot)[0].Id);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal("a", selector.Select(snapshot)[0].Id);
    }

    [Fact]
    public void CapacityEvictsLeastRecentlyUsedServiceRotationState()
    {
        var clock = new FakeTimeProvider();
        var selector = new WeightedRoundRobinServiceEndpointSelector(
            maximumServices: 1, stateRetention: TimeSpan.FromHours(1), timeProvider: clock);
        var catalog = Snapshot("catalog", clock, Endpoint("a", 3), Endpoint("b", 1));
        Assert.Equal("a", selector.Select(catalog)[0].Id);
        Assert.Equal("a", selector.Select(catalog)[0].Id);

        clock.Advance(TimeSpan.FromSeconds(1));
        selector.Select(Snapshot("other", clock, Endpoint("x", 3), Endpoint("y", 1)));

        Assert.Equal("a", selector.Select(catalog)[0].Id);
    }

    [Fact]
    public void EndpointCapacityRejectsOversizedSnapshotWithoutPoisoningLaterSelection()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector(maximumEndpoints: 2);
        var clock = TimeProvider.System;

        Assert.Throws<ResilienceConfigurationException>(() =>
            selector.Select(Snapshot("catalog", clock, Endpoint("a"), Endpoint("b"), Endpoint("c"))));

        Assert.Equal("a", selector.Select(Snapshot("catalog", clock, Endpoint("a"), Endpoint("b")))[0].Id);
    }

    [Fact]
    public void PreviouslyReturnedCandidateOrderIsAnImmutableCallSnapshot()
    {
        var selector = new WeightedRoundRobinServiceEndpointSelector();
        var snapshot = Snapshot("catalog", TimeProvider.System, Endpoint("a", 3), Endpoint("b", 1));
        var callCandidates = selector.Select(snapshot);
        var originalOrder = callCandidates.Select(endpoint => endpoint.Id).ToArray();

        for (var i = 0; i < 8; i++) selector.Select(snapshot);

        Assert.Equal(new[] { "a", "b" }, originalOrder);
        Assert.Equal(originalOrder, callCandidates.Select(endpoint => endpoint.Id));
    }

    [Fact]
    public async Task HttpClientUsesWeightedFirstSendAndFiltersIncompatibleHighWeightEndpoint()
    {
        const int requestCount = 100;
        var clock = TimeProvider.System;
        var source = new ConfigurationServiceEndpointSource(
            new DelegateServiceEndpointTrustPolicy((service, endpoint) =>
                service == "catalog" && endpoint.Origin.Host.EndsWith(".test", StringComparison.Ordinal)), clock);
        source.Publish(Snapshot("catalog", clock,
            Endpoint("a", 3), Endpoint("b", 1), Endpoint("incompatible", 100, contractRevision: "v2")), null);
        var firstSends = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBingResilience().AddBuiltInProvider().UseDefaultProvider("builtin");
        services.AddSingleton<IServiceEndpointSource>(source);
        var builder = services.AddHttpClient("catalog")
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(request => firstSends.Add(request.RequestUri!.Host)));
        builder.AddBingHttpResilience("catalog", options =>
        {
            options.UseDynamicRouting = true;
            options.Retry.MaxRetryAttempts = 0;
            options.CircuitBreaker.Enabled = false;
            options.Hedging.ValidateCustomTransport = _ => true;
        });
        builder.AddBingHttpDiscovery(options =>
        {
            options.ServiceName = "catalog";
            options.LogicalOrigin = new Uri("https://logical.test");
            options.ContractRevision = "v1";
            options.ConsistencyGroup = "stable";
            options.CredentialAudience = "catalog";
        });

        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("catalog");
        for (var i = 0; i < requestCount; i++)
        {
            using var response = await client.GetAsync("https://logical.test/items");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(requestCount, firstSends.Count);
        Assert.Equal(75, firstSends.Count(host => host == "a.test"));
        Assert.Equal(25, firstSends.Count(host => host == "b.test"));
        Assert.DoesNotContain("incompatible.test", firstSends);
    }

    [Fact]
    public void ExplicitOrderedSelectorRemainsAvailableWhenHttpDiscoveryRegistersDefaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServiceEndpointSelector, OrderedServiceEndpointSelector>();
        var builder = services.AddHttpClient("catalog");
        builder.AddBingHttpDiscovery(options =>
        {
            options.ServiceName = "catalog";
            options.LogicalOrigin = new Uri("https://logical.test");
            options.ContractRevision = "v1";
            options.ConsistencyGroup = "stable";
            options.CredentialAudience = "catalog";
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<OrderedServiceEndpointSelector>(provider.GetRequiredService<IServiceEndpointSelector>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IServiceEndpointSelector));
    }

    private static ServiceEndpoint Endpoint(string id, int weight = 1, string contractRevision = "v1") =>
        new(id, "1", new Uri("https://" + id + ".test"), contractRevision, "stable", "catalog", weight);

    private static ServiceEndpointSnapshot Snapshot(string service, TimeProvider clock, params ServiceEndpoint[] endpoints)
    {
        var now = clock.GetUtcNow();
        return new(service, "version-1", now, now.AddMinutes(1), now.AddMinutes(2), endpoints);
    }

    private sealed class RecordingHandler(Action<HttpRequestMessage> record) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            record(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }
}
