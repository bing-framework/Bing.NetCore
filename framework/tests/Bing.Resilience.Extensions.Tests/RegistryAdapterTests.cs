using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class RegistryAdapterTests
{
    private static readonly IServiceEndpointTrustPolicy Trust = new DelegateServiceEndpointTrustPolicy((_, e) => e.Origin.Host.StartsWith("10.0.0.", StringComparison.Ordinal));
    private static T Options<T>() where T : HttpRegistryEndpointOptions, new() => new()
    {
        ServiceName = "catalog", BaseAddress = new("https://registry.test/"), TrustBaseAddress = _ => true,
        ValidateCustomTransport = _ => true, CredentialAudience = "catalog", RefreshPeriod = TimeSpan.FromDays(1),
        TimeToLive = TimeSpan.FromSeconds(1), MaxStale = TimeSpan.FromSeconds(5), FailureBackoff = TimeSpan.FromSeconds(1)
    };
    private static string Consul(string host = "10.0.0.1", string node = "node-a", string status = "passing") => JsonSerializer.Serialize(new[] { new
    {
        Node = new { ID = node, Address = host, Datacenter = "dc-a" },
        Service = new { ID = "catalog-1", Service = "catalog", Address = "", Port = 8080, CreateIndex = 7, Meta = new { contractRevision = "v2" } },
        Checks = new[] { new { Status = status } }
    } });
    private static object Slice(string uid = "pod-1", bool ready = true, bool terminating = false, string slice = "slice-1") => new
    {
        metadata = new { uid = slice, @namespace = "default", labels = new Dictionary<string, string> { ["kubernetes.io/service-name"] = "catalog" } },
        ports = new[] { new { name = "http", port = 8080, protocol = "TCP" } },
        endpoints = new[] { new { addresses = new[] { "10.0.0.1" }, targetRef = new { uid }, conditions = new { ready, terminating } } }
    };
    private static string Kube(string version = "42", string next = "", params object[] slices) => JsonSerializer.Serialize(new
    { metadata = new { resourceVersion = version, @continue = next }, items = slices });

    [Fact]
    public async Task ConsulUsesNodeIdentityAddressFallbackAndFrozenOptions()
    {
        var options = Options<ConsulEndpointOptions>(); options.Datacenter = "dc-a";
        var handler = new Handler((r, _) =>
        {
            Assert.Equal("https://registry.test/v1/health/service/catalog?passing=true&dc=dc-a", r.RequestUri!.AbsoluteUri);
            return Task.FromResult(Json("[" + Consul()[1..^1] + "," + Consul("10.0.0.2", "node-b")[1..^1] + "]"));
        });
        using var source = new ConsulServiceEndpointSource(options, handler, Trust, TimeProvider.System);
        options.Datacenter = "evil"; options.ServiceName = "changed"; options.PassingOnly = false;
        var snapshot = await source.GetSnapshotAsync("catalog");
        Assert.Equal(2, snapshot.Endpoints.Count); Assert.NotEqual(snapshot.Endpoints[0].Id, snapshot.Endpoints[1].Id);
        Assert.Equal("7", snapshot.Endpoints[0].Incarnation); Assert.Equal("v2", snapshot.Endpoints[0].ContractRevision);
        Assert.Equal(new Uri("http://10.0.0.1:8080/"), snapshot.Endpoints[0].Origin);
    }

    [Fact]
    public async Task ConsulFiltersFailingChecksAndRequiresServiceIdentity()
    {
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler(Consul(status: "critical")), Trust, TimeProvider.System);
        Assert.Empty((await source.GetSnapshotAsync("catalog")).Endpoints);
        using var bad = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler(Consul().Replace("\"Service\":\"catalog\"", "\"Service\":\"other\"")), Trust, TimeProvider.System);
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await bad.GetSnapshotAsync("catalog"));
    }

    [Fact]
    public async Task NacosFiltersDisabledUnhealthyAndZeroWeightAndRequiresIncarnation()
    {
        var options = Options<NacosEndpointOptions>(); options.GroupName = "tenant group";
        var hosts = new[] { (true, true, 0.5), (false, true, 1.0), (true, false, 1.0), (true, true, 0.0) }
            .Select((x, i) => new { instanceId = "i" + i, ip = "10.0.0.1", port = 8080, healthy = x.Item1, enabled = x.Item2, weight = x.Item3, metadata = new { incarnation = "birth1" } });
        var json = JsonSerializer.Serialize(new { name = "tenant group@@catalog", hosts });
        using var source = new NacosServiceEndpointSource(options, new Handler((r, _) =>
        {
            Assert.Equal("/nacos/v1/ns/instance/list", r.RequestUri!.AbsolutePath);
            Assert.Contains("groupName=tenant%20group", r.RequestUri.Query);
            return Task.FromResult(Json(json));
        }), Trust, TimeProvider.System);
        options.GroupName = "changed";
        var endpoint = Assert.Single((await source.GetSnapshotAsync("catalog")).Endpoints);
        Assert.Equal(50, endpoint.Weight); Assert.Equal("birth1", endpoint.Incarnation);
        using var bad = new NacosServiceEndpointSource(Options<NacosEndpointOptions>(), new Handler(json.Replace("tenant group@@catalog", "catalog").Replace("incarnation", "unused")), Trust, TimeProvider.System);
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await bad.GetSnapshotAsync("catalog"));
    }

    [Fact]
    public async Task KubernetesPaginatesAuthenticatesAndDeduplicatesStablePodIdentity()
    {
        var clock = new FakeTimeProvider(); var count = 0; var auth = 0;
        var options = Options<KubernetesEndpointSliceOptions>(); options.PortName = "http";
        options.PrepareRequestAsync = (r, _) => { r.Headers.Authorization = new("Bearer", "test"); auth++; return ValueTask.CompletedTask; };
        using var source = new KubernetesEndpointSliceSource(options, new Handler((r, _) =>
        {
            Assert.Equal("Bearer test", r.Headers.Authorization!.ToString());
            var n = ++count;
            if (n == 1) return Task.FromResult(Json(Kube("42", "next+/", Slice())));
            if (n == 2) { Assert.Contains("continue=next%2B%2F", r.RequestUri!.Query); return Task.FromResult(Json(Kube("42", "", Slice(slice: "slice-2")))); }
            return Task.FromResult(Json(Kube("43", "", Slice(slice: "slice-3"))));
        }), Trust, clock);
        var first = await source.GetSnapshotAsync("catalog"); var identity = Assert.Single(first.Endpoints).StateKey;
        clock.Advance(TimeSpan.FromSeconds(2));
        var second = await source.GetSnapshotAsync("catalog");
        Assert.Equal(identity, Assert.Single(second.Endpoints).StateKey); Assert.Equal("43", second.SourceVersion); Assert.Equal(3, auth);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task KubernetesDoesNotRouteUnreadyOrTerminatingPods(bool ready, bool terminating)
    {
        using var source = new KubernetesEndpointSliceSource(Options<KubernetesEndpointSliceOptions>(), new Handler(Kube("1", "", Slice(ready: ready, terminating: terminating))), Trust, TimeProvider.System);
        Assert.Empty((await source.GetSnapshotAsync("catalog")).Endpoints);
    }

    [Fact]
    public async Task KubernetesRejectsInconsistentOrUnboundedPaginationWithoutPublishingPartialSnapshot()
    {
        foreach (var version in new[] { "42", "43" })
        {
            var count = 0; var options = Options<KubernetesEndpointSliceOptions>(); options.MaximumPages = 2;
            using var source = new KubernetesEndpointSliceSource(options, new Handler((_, _) => Task.FromResult(Json(++count == 1 ? Kube("42", "next", Slice()) : Kube(version, "next", Slice())))), Trust, TimeProvider.System);
            await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
        }
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(200)]
    public async Task AuthorizationLossOrAclFilteredListInvalidatesStaleCache(int status)
    {
        var clock = new FakeTimeProvider(); var count = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler((_, _) =>
        {
            var response = Json(Consul()); if (++count == 2) { response.StatusCode = (HttpStatusCode)status; response.Headers.Add("X-Consul-Results-Filtered-By-ACLs", "true"); }
            return Task.FromResult(response);
        }), Trust, clock);
        await source.GetSnapshotAsync("catalog"); clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
    }

    [Fact]
    public async Task TransportFailureUsesOnlyBoundedStaleSnapshot()
    {
        var clock = new FakeTimeProvider(); var count = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler((_, _) => ++count == 1 ? Task.FromResult(Json(Consul())) : Task.FromException<HttpResponseMessage>(new HttpRequestException())), Trust, clock);
        var original = await source.GetSnapshotAsync("catalog"); clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Same(original, await source.GetSnapshotAsync("catalog"));
        clock.Advance(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[{\"Node\":{\"ID\":\"node-a\",\"Address\":\"10.0.0.1\"},\"Service\":{\"Service\":\"catalog\",\"ID\":\"catalog-1\",\"Port\":0,\"CreateIndex\":1},\"Checks\":[] }]")]
    [InlineData("[{\"Node\":{\"ID\":\"node-a\",\"Address\":\"10.0.0.1\"},\"Service\":{\"Service\":\"catalog\",\"ID\":\"catalog-1\",\"Port\":\"bad\"},\"Checks\":[] }]")]
    public async Task InvalidRegistryDocumentInvalidatesPreviouslyValidSnapshot(string invalid)
    {
        var clock = new FakeTimeProvider(); var count = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler((_, _) => Task.FromResult(Json(++count == 1 ? Consul() : invalid))), Trust, clock);
        await source.GetSnapshotAsync("catalog"); clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
    }

    [Fact]
    public async Task OneCancelledWaiterDoesNotCancelSharedRefresh()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var sends = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler(async (_, token) =>
        { Interlocked.Increment(ref sends); entered.SetResult(true); await finish.Task.WaitAsync(token); return Json(Consul()); }), Trust, TimeProvider.System);
        using var cancel = new CancellationTokenSource();
        var first = source.GetSnapshotAsync("catalog", cancel.Token).AsTask(); await entered.Task;
        var second = source.GetSnapshotAsync("catalog").AsTask(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first); finish.SetResult(true);
        Assert.Single((await second).Endpoints); Assert.Equal(1, sends);
    }

    [Fact]
    public async Task RevocationAndOriginReuseRequireNewIncarnation()
    {
        var clock = new FakeTimeProvider(); var count = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler((_, _) => Task.FromResult(Json(Consul(++count == 1 ? "10.0.0.1" : "10.0.0.2")))), Trust, clock);
        var endpoint = Assert.Single((await source.GetSnapshotAsync("catalog")).Endpoints);
        source.Revoke(endpoint.Id, endpoint.Incarnation); Assert.True(source.IsRevoked("catalog", endpoint));
        Assert.Throws<ResilienceConfigurationException>(() => source.Revoke("unknown", "1"));
        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisposeCancelsRefreshAndHonorsTransportOwnership(bool ownsTransport)
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) =>
        { entered.SetResult(true); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Json(Consul()); });
        var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), handler, Trust, TimeProvider.System, ownsTransport);
        var waiting = source.GetSnapshotAsync("catalog").AsTask(); await entered.Task;
        source.Dispose(); source.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(ownsTransport ? 1 : 0, handler.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await source.GetSnapshotAsync("catalog"));
    }

    [Fact]
    public async Task SizeSignatureAndTransportGuardsRejectUnsafeDocuments()
    {
        foreach (var signature in new[] { true, false })
        {
            var options = Options<ConsulEndpointOptions>();
            if (signature) options.VerifyDocument = _ => false; else options.MaximumBytes = 2;
            using var source = new ConsulServiceEndpointSource(options, new Handler(Consul()), Trust, TimeProvider.System);
            await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await source.GetSnapshotAsync("catalog"));
        }
        using var transport = new SocketsHttpHandler();
        Assert.Throws<ResilienceConfigurationException>(() => new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), transport, Trust, TimeProvider.System));
    }

    [Fact]
    public async Task NotModifiedKeepsVersionAndAuthCannotRedirectCredentials()
    {
        var clock = new FakeTimeProvider(); var count = 0;
        using var source = new ConsulServiceEndpointSource(Options<ConsulEndpointOptions>(), new Handler((r, _) =>
        {
            if (++count == 1) { var response = Json(Consul()); response.Headers.ETag = new EntityTagHeaderValue("\"one\""); return Task.FromResult(response); }
            Assert.Equal("\"one\"", Assert.Single(r.Headers.IfNoneMatch).Tag);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        }), Trust, clock);
        var first = await source.GetSnapshotAsync("catalog"); clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(first.SourceVersion, (await source.GetSnapshotAsync("catalog")).SourceVersion);
        var options = Options<ConsulEndpointOptions>(); options.PrepareRequestAsync = (r, _) => { r.RequestUri = new("https://evil.test/"); return ValueTask.CompletedTask; };
        using var blocked = new ConsulServiceEndpointSource(options, new Handler((_, _) => throw new Exception("must not send")), Trust, clock);
        await Assert.ThrowsAsync<ResilienceDiscoveryException>(async () => await blocked.GetSnapshotAsync("catalog"));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Disposals { get; private set; }
        public Handler(string json) : this((_, _) => Task.FromResult(Json(json))) { }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}
