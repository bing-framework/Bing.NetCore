using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class ManifestTests
{
    [Fact]
    public async Task HttpManifestVerifiesSignatureCachesAndRefreshesNewRevision()
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        var signatures = 0;
        var source = CreateHttpSource(clock, request =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
                return ManifestResponse(Manifest(clock, "manifest-v5", 5, Endpoint("one")), "\"manifest-v5\"");
            Assert.Equal("\"manifest-v5\"", Assert.Single(request.Headers.IfNoneMatch).Tag);
            return ManifestResponse(Manifest(clock, "manifest-v6", 6, Endpoint("two")), "\"manifest-v6\"");
        }, _ =>
        {
            Interlocked.Increment(ref signatures);
            return true;
        }, minimumRevision: 5, ttl: TimeSpan.FromSeconds(10));
        using (source)
        {
            var first = await source.GetAsync("catalog");
            var cached = await source.GetAsync("catalog");
            Assert.Equal(5, first.Revision);
            Assert.Equal(first, cached);
            Assert.Equal(1, calls);

            clock.Advance(TimeSpan.FromSeconds(11));
            var refreshed = await source.GetAsync("catalog");

            Assert.Equal(6, refreshed.Revision);
            Assert.Equal("manifest-v6", refreshed.Version);
            Assert.Equal(2, calls);
            Assert.Equal(2, signatures);
        }
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("contract")]
    [InlineData("digest")]
    public async Task HttpManifestRejectsInvalidSignatureContractAndDigest(string invalidPart)
    {
        var clock = new FakeTimeProvider();
        var signatureCalls = 0;
        var manifest = invalidPart switch
        {
            "contract" => Manifest(clock, "manifest-v1", 1, Endpoint("one")) with { ContractRevision = "v2" },
            "digest" => Manifest(clock, "manifest-v1", 1, Endpoint("one")) with { Digest = "not-a-digest" },
            _ => Manifest(clock, "manifest-v1", 1, Endpoint("one"))
        };
        using var source = CreateHttpSource(clock, _ => ManifestResponse(manifest), _ =>
        {
            Interlocked.Increment(ref signatureCalls);
            return invalidPart != "signature";
        });

        await Assert.ThrowsAsync<ResilienceDiscoveryException>(() => source.GetAsync("catalog").AsTask());
        Assert.Equal(1, signatureCalls);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    public async Task HttpManifestRejectsRevisionRollbackAfterCacheExpiry(long changedRevision)
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        using var source = CreateHttpSource(clock, _ =>
        {
            var call = Interlocked.Increment(ref calls);
            return ManifestResponse(call == 1
                ? Manifest(clock, "manifest-v10", 10, Endpoint("one"))
                : Manifest(clock, "manifest-v9", changedRevision, Endpoint("one")));
        }, _ => true, minimumRevision: 10, ttl: TimeSpan.FromSeconds(5));

        Assert.Equal(10, (await source.GetAsync("catalog")).Revision);
        clock.Advance(TimeSpan.FromSeconds(6));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(() => source.GetAsync("catalog").AsTask());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task HttpManifestExpiresIndependentlyOfLongerDirectoryCache()
    {
        var clock = new FakeTimeProvider();
        using var source = CreateHttpSource(clock, _ => ManifestResponse(
            Manifest(clock, "manifest-short-lived", 1, TimeSpan.FromSeconds(1), Endpoint("one"))),
            _ => true, ttl: TimeSpan.FromMinutes(5));

        Assert.Equal("manifest-short-lived", (await source.GetAsync("catalog")).Version);
        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(() => source.GetAsync("catalog").AsTask());
    }

    [Fact]
    public async Task ManifestCandidatesAreTrustedDiscoveryIntersectionAndCannotAddOrReviveEndpoints()
    {
        var clock = new FakeTimeProvider();
        var trust = Trust();
        var shared = Endpoint("shared");
        var revoked = Endpoint("revoked");
        var contractDriftInDiscovery = Endpoint("contract-drift", contract: "v2");
        var discoveryOnly = Endpoint("discovery-only");
        var discovery = new ConfigurationServiceEndpointSource(trust, clock);
        discovery.Publish(Snapshot(clock, "discovery-v1", shared, revoked, contractDriftInDiscovery, discoveryOnly), null);
        discovery.Revoke("catalog", revoked.Id, revoked.Incarnation);
        var manifestOnly = Endpoint("manifest-only");
        var contractDriftInManifest = Endpoint("contract-drift", contract: "v1");
        var manifest = new StaticManifestSource(Manifest(clock, "manifest-v4", 4,
            shared, revoked, contractDriftInManifest, manifestOnly));
        var source = new ManifestServiceEndpointSource(discovery, manifest, trust, clock);

        var snapshot = await source.GetSnapshotAsync("catalog");

        Assert.Equal(new[] { shared }, snapshot.Endpoints);
        Assert.DoesNotContain(manifestOnly, snapshot.Endpoints);
        Assert.DoesNotContain(discoveryOnly, snapshot.Endpoints);
        Assert.DoesNotContain(revoked, snapshot.Endpoints);
        Assert.DoesNotContain(contractDriftInDiscovery, snapshot.Endpoints);
        Assert.Equal("discovery-v1:4", snapshot.SourceVersion);
    }

    private static HttpEndpointManifestSource CreateHttpSource(FakeTimeProvider clock,
        Func<HttpRequestMessage, HttpResponseMessage> response, Func<ReadOnlyMemory<byte>, bool> verify,
        long minimumRevision = 0, TimeSpan? ttl = null)
    {
        var options = new HttpEndpointManifestOptions
        {
            MinimumRevision = minimumRevision,
            ConsistencyGroup = "stable",
            ContractRevision = "v1",
            VerifySignature = verify
        };
        options.Directory.ServiceName = "catalog";
        options.Directory.DirectoryUri = new Uri("https://directory.test/catalog/manifest");
        options.Directory.TrustDirectory = uri => uri.Host == "directory.test";
        options.Directory.ValidateCustomTransport = handler => handler is ManifestHandler;
        options.Directory.TimeToLive = ttl ?? TimeSpan.FromMinutes(1);
        options.Directory.MaxStale = TimeSpan.Zero;
        options.Directory.RefreshPeriod = TimeSpan.FromHours(1);
        options.Directory.RequestTimeout = TimeSpan.FromSeconds(2);
        options.Directory.FailureBackoff = TimeSpan.FromSeconds(1);
        return new HttpEndpointManifestSource(options, new ManifestHandler(response), Trust(), clock, disposeTransport: true);
    }

    private static HttpResponseMessage ManifestResponse(EndpointManifest manifest, string? etag = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(manifest), Encoding.UTF8, "application/json")
        };
        if (etag is not null) response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    }

    private static EndpointManifest Manifest(FakeTimeProvider clock, string version, long revision,
        params ServiceEndpoint[] endpoints) => Manifest(clock, version, revision, TimeSpan.FromMinutes(30), endpoints);

    private static EndpointManifest Manifest(FakeTimeProvider clock, string version, long revision,
        TimeSpan validFor, params ServiceEndpoint[] endpoints) => new(1, "catalog", version, revision,
        clock.GetUtcNow() + validFor, "stable", "v1", new string('A', 64), endpoints);

    private static ServiceEndpointSnapshot Snapshot(FakeTimeProvider clock, string version, params ServiceEndpoint[] endpoints) =>
        new("catalog", version, clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1), clock.GetUtcNow().AddMinutes(2), endpoints);

    private static ServiceEndpoint Endpoint(string id, string contract = "v1") =>
        new(id, "1", new Uri("https://" + id + ".test"), contract, "stable", "catalog");

    private static IServiceEndpointTrustPolicy Trust() => new DelegateServiceEndpointTrustPolicy(
        (service, endpoint) => service == "catalog" && endpoint.Origin.Host.EndsWith(".test", StringComparison.Ordinal));

    private sealed class ManifestHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }

    private sealed class StaticManifestSource(EndpointManifest manifest) : IEndpointManifestSource
    {
        public ValueTask<EndpointManifest> GetAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(manifest);
        }
    }
}
