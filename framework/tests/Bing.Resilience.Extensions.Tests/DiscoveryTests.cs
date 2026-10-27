using System.Net;
using System.Text;
using System.Text.Json;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Discovery;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class DiscoveryTests
{
    private static ServiceEndpoint Endpoint(string id, string incarnation = "1", string? host = null) =>
        new(id, incarnation, new Uri("https://" + (host ?? id) + ".test"), "v1", "stable", "catalog");
    private static ServiceEndpointSnapshot Snapshot(TimeProvider clock, string version, params ServiceEndpoint[] endpoints) =>
        new("catalog", version, clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1), clock.GetUtcNow().AddMinutes(2), endpoints);
    private static IServiceEndpointTrustPolicy Trust() => new DelegateServiceEndpointTrustPolicy((service, e) => service == "catalog" && e.Origin.Host.EndsWith(".test", StringComparison.Ordinal));

    [Fact]
    public async Task ConfigurationUsesCasRejectsIdentityReuseAndHonorsEmptyAndExpiry()
    {
        var clock = new FakeTimeProvider();
        var source = new ConfigurationServiceEndpointSource(Trust(), clock, 2);
        source.Publish(Snapshot(clock, "a", Endpoint("one")), null);
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => Task.Run(() => source.Publish(Snapshot(clock, "b", Endpoint("two")), "wrong")));
        Assert.Throws<ResilienceConfigurationException>(() => source.Publish(Snapshot(clock, "b", Endpoint("one", host: "other")), "a"));
        var original = await source.GetSnapshotAsync("catalog");
        source.Revoke("catalog", "one", "1");
        Assert.True(source.IsRevoked("catalog", original.Endpoints[0]));
        source.Publish(Snapshot(clock, "b"), "a");
        Assert.Empty((await source.GetSnapshotAsync("catalog")).Endpoints);
        Assert.Single(original.Endpoints);
        clock.Advance(TimeSpan.FromMinutes(3));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(() => source.GetSnapshotAsync("catalog").AsTask());
    }

    [Fact]
    public async Task ConfigurationRevokeRetiredIdentityBlocksOldSnapshotAndExpiresWithStaleWindow()
    {
        var clock = new FakeTimeProvider();
        var source = new ConfigurationServiceEndpointSource(Trust(), clock, 4);
        source.Publish(Snapshot(clock, "a", Endpoint("old")), null);
        var original = await source.GetSnapshotAsync("catalog");
        clock.Advance(TimeSpan.FromSeconds(1));
        source.Publish(Snapshot(clock, "b", Endpoint("new")), "a");

        source.Revoke("catalog", "old", "1");
        Assert.True(source.IsRevoked("catalog", original.Endpoints[0]));
        Assert.False(source.IsRevoked("catalog", (await source.GetSnapshotAsync("catalog")).Endpoints[0]));
        Assert.Throws<ResilienceConfigurationException>(() => source.Revoke("catalog", "unknown", "1"));

        clock.Advance(TimeSpan.FromMinutes(3));
        source.Publish(Snapshot(clock, "c", Endpoint("old")), "b");
        Assert.False(source.IsRevoked("catalog", original.Endpoints[0]));
    }
    [Fact]
    public async Task HttpRetryKeepsSnapshotWhileRefreshChangesFutureCallsAndRefreshesAuthentication()
    {
        var clock = TimeProvider.System;
        var source = new ConfigurationServiceEndpointSource(Trust(), clock);
        source.Publish(Snapshot(clock, "a", Endpoint("one"), Endpoint("two")), null);
        var sends = new List<(string Host, string Path, string? Auth)>();
        var services = new ServiceCollection();
        services.AddBingResilience().AddBuiltInProvider().UseDefaultProvider("builtin");
        services.AddSingleton<IServiceEndpointSource>(source);
        services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(() => new Handler(request =>
        {
            sends.Add((request.RequestUri!.Host, request.RequestUri.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (sends.Count == 1)
            {
                source.Publish(Snapshot(clock, "b", Endpoint("three")), "a");
                return new(HttpStatusCode.ServiceUnavailable);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("ok") };
        })).AddBingHttpResilience("catalog", o =>
        {
            o.UseDynamicRouting = true; o.CanRetryRequest = _ => true; o.Retry.MaxRetryAttempts = 1;
            o.Retry.Delay = TimeSpan.Zero; o.Retry.UseJitter = false; o.CircuitBreaker.Enabled = false;
            o.Hedging.ValidateCustomTransport = _ => true;
        }).AddBingHttpDiscovery(o =>
        {
            o.ServiceName = "catalog"; o.LogicalOrigin = new Uri("https://logical.test");
            o.ContractRevision = "v1"; o.ConsistencyGroup = "stable"; o.CredentialAudience = "catalog"; o.StateRetention = TimeSpan.Zero;
        }).AddHttpMessageHandler(() => new Signer());
        using var host = services.BuildServiceProvider();
        using var client = host.GetRequiredService<IHttpClientFactory>().CreateClient("catalog");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://logical.test/items?take=1");
        request.Headers.Authorization = new("Bearer", "original");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var second = await client.GetAsync("https://logical.test/items?take=2");
        Assert.Equal(new[] { "one.test", "two.test", "three.test" }, sends.Select(x => x.Host));
        Assert.Equal(new[] { "/items?take=1", "/items?take=1", "/items?take=2" }, sends.Select(x => x.Path));
        Assert.All(sends, x => Assert.Equal(x.Host, x.Auth));
    }
    [Fact]
    public async Task DirectoryBoundsContentSupportsEtagAndFailsClosedAfterStaleBudget()
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        using var source = new HttpDirectoryServiceEndpointSource(new()
        {
            ServiceName = "catalog", DirectoryUri = new Uri("https://directory.test/services/catalog"),
            TrustDirectory = uri => uri.Host == "directory.test", ValidateCustomTransport = _ => true,
            TimeToLive = TimeSpan.FromSeconds(10), MaxStale = TimeSpan.FromSeconds(10),
            RefreshPeriod = TimeSpan.FromHours(1), FailureBackoff = TimeSpan.FromSeconds(1)
        }, new Handler(request =>
        {
            calls++;
            if (calls == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                    new HttpEndpointDirectoryDocument("catalog", "rev-a", new[] { Endpoint("one") })), Encoding.UTF8, "application/json") };
                response.Headers.ETag = new("\"one\""); return response;
            }
            Assert.Equal("\"one\"", request.Headers.IfNoneMatch.Single().Tag);
            if (calls == 2) return new(HttpStatusCode.NotModified);
            return new(HttpStatusCode.ServiceUnavailable);
        }), Trust(), clock);
        Assert.Single((await source.GetSnapshotAsync("catalog")).Endpoints);
        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.Single((await source.GetSnapshotAsync("catalog")).Endpoints);
        Assert.Equal(2, calls);
        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.Single((await source.GetSnapshotAsync("catalog")).Endpoints);
        clock.Advance(TimeSpan.FromSeconds(11));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(() => source.GetSnapshotAsync("catalog").AsTask());
    }
    [Fact]
    public async Task DirectoryRevokeRetiredIdentityStillBlocksOldSnapshot()
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        using var source = new HttpDirectoryServiceEndpointSource(new()
        {
            ServiceName = "catalog", DirectoryUri = new Uri("https://directory.test/services/catalog"),
            TrustDirectory = uri => uri.Host == "directory.test", ValidateCustomTransport = _ => true,
            TimeToLive = TimeSpan.FromSeconds(1), MaxStale = TimeSpan.FromMinutes(1),
            RefreshPeriod = TimeSpan.FromHours(1), FailureBackoff = TimeSpan.FromSeconds(1)
        }, new Handler(_ =>
        {
            calls++;
            var endpoint = calls == 1 ? Endpoint("old") : Endpoint("new");
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(
                    new HttpEndpointDirectoryDocument("catalog", "rev-" + calls, new[] { endpoint })), Encoding.UTF8, "application/json")
            };
        }), Trust(), clock);

        var original = await source.GetSnapshotAsync("catalog");
        clock.Advance(TimeSpan.FromSeconds(2));
        var current = await source.GetSnapshotAsync("catalog");

        source.Revoke("old", "1");

        Assert.True(source.IsRevoked("catalog", original.Endpoints[0]));
        Assert.False(source.IsRevoked("catalog", current.Endpoints[0]));
        Assert.Throws<ResilienceConfigurationException>(() => source.Revoke("unknown", "1"));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
    private sealed class Signer : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new("Bearer", request.RequestUri!.Host);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
