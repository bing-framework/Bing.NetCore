using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class DefinitionRecoveryTests
{
    [Fact]
    public async Task SlowPrimaryBodyLosesToFullyValidatedBackupAndIsDisposed()
    {
        var slow = new SlowContent();
        var sends = 0;
        await using var services = Create(new Handler((request, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = request.RequestUri!.Host == "primary.test" ? slow : Json("backup") });
        }));
        var source = services.GetRequiredService<IBingDynamicApiDefinitionSource>();
        var definition = await source.GetAsync(Request(), CancellationToken.None);
        Assert.Equal("backup", definition.Services.Single().ServiceName);
        Assert.Equal(2, sends);
        Assert.True(slow.Cancelled);
        Assert.Equal(1, slow.Disposed);
    }

    [Fact]
    public async Task InvalidPrimaryIsNeverCachedAndBackupMustPassContract()
    {
        var sends = 0;
        await using var services = Create(new Handler((request, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri!.Host == "primary.test"
                ? new StringContent("{bad json") : Json("valid") });
        }));
        var source = services.GetRequiredService<IBingDynamicApiDefinitionSource>();
        Assert.Equal("valid", (await source.GetAsync(Request(), default)).Services.Single().ServiceName);
        Assert.Equal("valid", (await source.GetAsync(Request(), default)).Services.Single().ServiceName);
        Assert.Equal(2, sends);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task AccessDenialStopsBeforeLaunchingBackup(int status)
    {
        var sends = 0;
        await using var services = Create(new Handler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        }), configure: o => o.Delay = TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<Exception>(() => services.GetRequiredService<IBingDynamicApiDefinitionSource>().GetAsync(Request(), default).AsTask());
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task RetryAfterBeyondBudgetNeverStartsAnotherSend()
    {
        var sends = 0;
        await using var services = Create(new Handler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
            return Task.FromResult(response);
        }), budget: TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAsync<TimeoutException>(() => services.GetRequiredService<IBingDynamicApiDefinitionSource>().GetAsync(Request(), default).AsTask());
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task WaiterCancellationDoesNotCancelSharedFetchAndScopesDoNotShareCache()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = 0;
        await using var services = Create(new Handler(async (_, token) =>
        {
            Interlocked.Increment(ref sends); entered.TrySetResult(true);
            await release.Task.WaitAsync(token);
            return new(HttpStatusCode.OK) { Content = Json("catalog") };
        }), configure: o => { o.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.SequentialRetry; o.MaxAttempts = 1; });
        var source = services.GetRequiredService<IBingDynamicApiDefinitionSource>();
        using var cancellation = new CancellationTokenSource();
        var first = source.GetAsync(Request(), cancellation.Token).AsTask();
        await entered.Task;
        var second = source.GetAsync(Request(), default).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult(true);
        await second;
        Assert.Equal(1, sends);
        await source.GetAsync(Request() with { TenantId = "other-scope" }, default);
        Assert.Equal(2, sends);
    }

    [Fact]
    public async Task FailedSharedFetchIsEvictedAndNextCallCanDownloadAgain()
    {
        var sends = 0;
        await using var services = Create(new Handler((_, _) => Task.FromResult(
            Interlocked.Increment(ref sends) == 1 ? new(HttpStatusCode.ServiceUnavailable) : new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("recovered") })),
            configure: o => { o.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.SequentialRetry; o.MaxAttempts = 1; });
        var source = services.GetRequiredService<IBingDynamicApiDefinitionSource>();
        await Assert.ThrowsAnyAsync<Exception>(() => source.GetAsync(Request(), default).AsTask());
        Assert.Equal("recovered", (await source.GetAsync(Request(), default)).Services.Single().ServiceName);
        Assert.Equal(2, sends);
    }

    [Fact]
    public async Task SignatureFailureIsTerminalAndDoesNotPopulateCache()
    {
        var sends = 0;
        await using var services = Create(new Handler((_, _) =>
        { Interlocked.Increment(ref sends); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("signed") }); }),
            configure: o => { o.VerifySignature = _ => false; o.Delay = TimeSpan.FromSeconds(1); });
        var source = services.GetRequiredService<IBingDynamicApiDefinitionSource>();
        await Assert.ThrowsAnyAsync<Exception>(() => source.GetAsync(Request(), default).AsTask());
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task BodyLimitAndApiVersionPreventInvalidWinner()
    {
        await using var services = Create(new Handler((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.Host == "primary.test" ? new StringContent(new string('x', 1024)) : Json("valid")
        })), configure: o => { o.MaxWireBytes = 256; o.MaxDecodedBytes = 256; });
        Assert.Equal("valid", (await services.GetRequiredService<IBingDynamicApiDefinitionSource>().GetAsync(Request(), default)).Services.Single().ServiceName);
    }

    [Fact]
    public async Task AuthenticationUsesIndependentScopesForEveryBranch()
    {
        var identities = new List<Guid>();
        var disposed = 0;
        var collection = new ServiceCollection();
        collection.AddScoped(_ => new AttemptScope(() => Interlocked.Increment(ref disposed)));
        await using var services = Create(new Handler((request, _) => Task.FromResult(new HttpResponseMessage(
            request.RequestUri!.Host == "primary.test" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = Json("valid") })),
            collection, o => o.PrepareRequestAsync = (sp, _, request, _) =>
            {
                var scope = sp.GetRequiredService<AttemptScope>();
                lock (identities) identities.Add(scope.Id);
                request.Headers.Add("X-Attempt-Signature", scope.Id.ToString());
                return default;
            });
        await services.GetRequiredService<IBingDynamicApiDefinitionSource>().GetAsync(Request(), default);
        Assert.Equal(2, identities.Distinct().Count());
        Assert.Equal(2, disposed);
    }

    private sealed class AttemptScope(Action dispose) : IDisposable
    { public Guid Id { get; } = Guid.NewGuid(); public void Dispose() => dispose(); }
    private static ServiceProvider Create(HttpMessageHandler transport, ServiceCollection? services = null,
        Action<BingDynamicApiDefinitionRecoveryOptions>? configure = null, TimeSpan? budget = null)
    {
        services ??= new();
        services.AddBingResilience().AddBuiltInProvider().UseDefaultProvider("builtin");
        services.AddBingDynamicApiClient(o => o.AddRemoteService("catalog", r => r.BaseUrl = "http://primary.test/"));
        services.AddBingDynamicApiClientResilience("catalog", o =>
        {
            o.DefinitionTimeout = budget ?? TimeSpan.FromSeconds(3);
            o.Definition.Enabled = true;
            o.Definition.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.Hedging;
            o.Definition.ServiceName = "catalog"; o.Definition.CompatibilityGroup = "v1"; o.Definition.ContractRevision = "r1";
            o.Definition.Delay = TimeSpan.FromMilliseconds(30);
            o.Definition.Endpoints.Add(new("primary", new Uri("http://primary.test/api/dynamic-api/v1/definition")));
            o.Definition.Endpoints.Add(new("backup", new Uri("http://backup.test/api/dynamic-api/v1/definition")));
            o.Definition.TrustEndpoint = uri => uri.Host is "primary.test" or "backup.test";
            o.Definition.ValidateIdentity = (_, _) => true;
            o.Definition.CaptureAccess = request => new(request.TenantId, "catalog", new Dictionary<string, string>());
            o.Definition.ConfigureTransport = builder => builder.ConfigurePrimaryHttpMessageHandler(() => transport);
            o.Definition.ValidateCustomTransport = _ => true;
            configure?.Invoke(o.Definition);
        });
        return services.BuildServiceProvider();
    }
    private static BingDynamicApiDefinitionRequest Request() => new("catalog", "catalog", "v1", "http://primary.test/", "scope-one", "zh-CN",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    private static StringContent Json(string name) => new(JsonSerializer.Serialize(new
    {
        SchemaVersion = 1, ApiVersion = "v1", Services = new[] { new { ServiceName = name, ServiceTypeName = "ITest", Methods = Array.Empty<object>() } }
    }), Encoding.UTF8, "application/json");
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    private sealed class SlowContent : HttpContent
    {
        internal bool Cancelled; internal int Disposed;
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new SlowStream(this));
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) Interlocked.Increment(ref Disposed); base.Dispose(disposing); }
        private sealed class SlowStream(SlowContent owner) : Stream
        {
            public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { owner.Cancelled = true; throw; }
                return 0;
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
