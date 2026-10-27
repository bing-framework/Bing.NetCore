using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Replay;
using Bing.Resilience;
using Bing.Resilience.Operations;
using Bing.Resilience.Replay;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class SyntheticResponseTests
{
    private static readonly HttpRequestOptionsKey<bool> IsStream = BingHttpOperationMetadata.IsStreamResult;
    private static readonly HttpRequestOptionsKey<bool> IsDefinition = BingHttpOperationMetadata.IsDefinition;

    [Fact]
    public async Task CachedSuccessDoesNotEraseCircuitFailuresAndObserverReportsSource()
    {
        var operation = ReadOperation("catalog.cached");
        var sends = 0;
        var policyCalls = 0;
        var observer = new RecordingObserver();
        var transport = new DelegateHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var policy = new DelegatePolicy((request, _, _) =>
        {
            Interlocked.Increment(ref policyCalls);
            return ValueTask.FromResult<HttpSyntheticResponse?>(Cached(request, "cache:catalog-v3"));
        });
        using var host = CreateClient("synthetic-circuit", transport, new[] { operation }, policy, observer, options =>
        {
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(5);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(3);
        });

        for (var index = 0; index < 3; index++)
        {
            using var request = ReadRequest(operation);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(2, sends);
        Assert.Equal(3, policyCalls);
        Assert.Equal(3, observer.Observations.Count);
        Assert.All(observer.Observations, observation =>
        {
            Assert.True(observation.IsSynthetic);
            Assert.Equal(HttpSyntheticResponseKind.Cached, observation.Kind);
            Assert.Equal("cache:catalog-v3", observation.SourceId);
            Assert.Equal(operation, observation.Operation);
        });
    }

    [Fact]
    public async Task ObserverFailureDoesNotDiscardSelectedResponse()
    {
        var operation = ReadOperation("catalog.observer-failure");
        var transport = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("response-is-owned-by-caller")
        }));
        using var host = CreateClient("synthetic-observer-failure", transport, new[] { operation },
            new DelegatePolicy((_, _, _) => ValueTask.FromResult<HttpSyntheticResponse?>(null)), new ThrowingObserver());
        using var request = ReadRequest(operation);

        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("response-is-owned-by-caller", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnapprovedStreamAndDefinitionFailuresAreNotReplaced()
    {
        var approved = ReadOperation("catalog.approved");
        var unapproved = ReadOperation("catalog.unapproved");
        var policyCalls = 0;
        var sends = 0;
        var transport = new DelegateHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var policy = new DelegatePolicy((request, _, _) =>
        {
            Interlocked.Increment(ref policyCalls);
            return ValueTask.FromResult<HttpSyntheticResponse?>(Cached(request, "cache:must-not-be-used"));
        });
        using var host = CreateClient("synthetic-eligibility", transport, new[] { approved }, policy);

        using (var request = ReadRequest(unapproved))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using (var request = ReadRequest(approved))
        {
            request.Options.Set(IsStream, true);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        using (var request = ReadRequest(approved))
        {
            request.Options.Set(IsDefinition, true);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        Assert.Equal(3, sends);
        Assert.Equal(0, policyCalls);
    }

    [Fact]
    public async Task CallerCancellationWinsBeforeSyntheticPolicyCanReplaceFailure()
    {
        var operation = ReadOperation("catalog.cancelled");
        var policyCalls = 0;
        var transport = new DelegateHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        var policy = new DelegatePolicy((request, _, _) =>
        {
            Interlocked.Increment(ref policyCalls);
            return ValueTask.FromResult<HttpSyntheticResponse?>(Cached(request, "cache:cancelled"));
        });
        using var host = CreateClient("synthetic-cancel", transport, new[] { operation }, policy, configure: options =>
        {
            options.RequestTimeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout = TimeSpan.FromSeconds(4);
            options.CircuitBreaker.Enabled = false;
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        using var request = ReadRequest(operation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Client.SendAsync(request, cancellation.Token));
        Assert.Equal(0, policyCalls);
    }

    [Fact]
    public async Task ReplacingFailureDisposesOriginalOnceAndTransfersReplacementToCaller()
    {
        var operation = ReadOperation("catalog.ownership");
        var originalContent = new TrackingContent("remote failure");
        var replacementContent = new TrackingContent("cached body");
        var transport = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = originalContent
        }));
        var policy = new DelegatePolicy((request, _, _) => ValueTask.FromResult<HttpSyntheticResponse?>(Cached(
            request, "cache:ownership", new HttpResponseMessage(HttpStatusCode.OK) { Content = replacementContent })));
        using var host = CreateClient("synthetic-ownership", transport, new[] { operation }, policy);
        using var request = ReadRequest(operation);

        var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, originalContent.DisposeCount);
        Assert.Equal(0, replacementContent.DisposeCount);
        response.Dispose();
        Assert.Equal(1, replacementContent.DisposeCount);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("contract")]
    [InlineData("scope")]
    [InlineData("status")]
    [InlineData("expired")]
    public async Task CachedResponseRequiresMatchingScopeContractAndFreshHttp200(string invalidPart)
    {
        var operation = ReadOperation("catalog.cache-contract");
        var originalContent = new TrackingContent("remote failure");
        var replacementContent = new TrackingContent("invalid cache entry");
        var transport = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = originalContent
        }));
        var policy = new DelegatePolicy((request, _, _) =>
        {
            var status = invalidPart == "status" ? HttpStatusCode.PartialContent : HttpStatusCode.OK;
            var validUntil = invalidPart == "expired" ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddMinutes(1);
            var replacement = new HttpSyntheticResponse(new HttpResponseMessage(status) { Content = replacementContent },
                HttpSyntheticResponseKind.Cached,
                invalidPart == "operation" ? request.Operation.Name + ".other" : request.Operation.Name,
                invalidPart == "contract" ? "v2" : request.Operation.ContractVersion,
                invalidPart == "scope" ? "tenant-other" : request.AccessScope,
                "cache:invalid", validUntil);
            return ValueTask.FromResult<HttpSyntheticResponse?>(replacement);
        });
        using var host = CreateClient("synthetic-invalid-cache-" + invalidPart, transport, new[] { operation }, policy);
        using var request = ReadRequest(operation);

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));

        Assert.Equal(1, originalContent.DisposeCount);
        Assert.Equal(1, replacementContent.DisposeCount);
    }

    [Fact]
    public async Task DurableAcceptedRequiresMatchingCommittedReceiptAndAddsOperationId()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.synthetic-accepted");
        var proof = new ProofHolder();
        var transport = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var policy = DurableAcceptedPolicy(proof, new TrackingContent("accepted"));
        using var host = await CreateDurableWriteHostAsync(database, operation, transport, policy, proof);
        var receipt = proof.Value!;
        Assert.Equal(OperationFingerprint.Hash("durable-body"), receipt.Fingerprint);

        using var request = WriteRequest(operation, "durable-key", "durable-body");
        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(receipt.OperationId, Assert.Single(response.Headers.GetValues("X-Bing-Operation-Id")));
        Assert.Equal(new Uri("https://orders.test/operations/" + receipt.OperationId), response.Headers.Location);
        Assert.Equal(1, host.TransportSends);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task DurableAcceptedRejectsMissingOrMismatchedCommitProof(bool provideProof, bool mismatchKey, bool mismatchBody)
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.synthetic-reject");
        var proof = new ProofHolder();
        var originalContent = new TrackingContent("remote failure");
        var replacementContent = new TrackingContent("unverified acceptance");
        var sends = 0;
        var transport = new DelegateHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = originalContent });
        });
        var policy = DurableAcceptedPolicy(proof, replacementContent, includeProof: provideProof);
        using var host = await CreateDurableWriteHostAsync(database, operation, transport, policy, proof);
        using var request = WriteRequest(operation, mismatchKey ? "other-key" : "durable-key",
            mismatchBody ? "different-body" : "durable-body");

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));

        Assert.Equal(1, sends);
        Assert.Equal(1, originalContent.DisposeCount);
        Assert.Equal(1, replacementContent.DisposeCount);
    }

    private static SyntheticClientHost CreateClient(string name, HttpMessageHandler transport,
        IReadOnlyCollection<ResilienceOperationDescriptor> operations, IHttpSyntheticResponsePolicy policy,
        IHttpResilienceResponseObserver? observer = null, Action<BingHttpResilienceOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBingResilience().AddBuiltInProvider("builtin").UseDefaultProvider("builtin");
        services.AddBingResilienceOperations(operations.ToArray());
        return AddHttpClient(services, name, transport, operations, policy, observer, configure, addReplay: false);
    }

    private static async Task<SyntheticClientHost> CreateDurableWriteHostAsync(DurableSqliteDatabase database,
        ResilienceOperationDescriptor operation, HttpMessageHandler transport, IHttpSyntheticResponsePolicy policy,
        ProofHolder proof)
    {
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingResilience().AddBuiltInProvider("builtin").UseDefaultProvider("builtin");
        services.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(true),
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name, new NoopReplayHandler()));
        var clientHost = AddHttpClient(services, "synthetic-durable", transport, new[] { operation }, policy,
            observer: null, configure: null, addReplay: true);
        var receipt = await clientHost.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(
            new ReplayCommand("synthetic-replay-1", operation.Name, operation.ContractVersion, "orders", "1", "tenant-test",
                "durable-key", "durable-body", DateTimeOffset.UtcNow.AddHours(1)));
        proof.Value = receipt;
        return clientHost;
    }

    private static SyntheticClientHost AddHttpClient(ServiceCollection services, string name, HttpMessageHandler transport,
        IReadOnlyCollection<ResilienceOperationDescriptor> operations, IHttpSyntheticResponsePolicy policy,
        IHttpResilienceResponseObserver? observer, Action<BingHttpResilienceOptions>? configure, bool addReplay)
    {
        var sends = new SendCounterHandler(transport);
        var builder = services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => sends);
        builder.AddBingHttpResilience("synthetic-response", options =>
        {
            options.RequestTimeout = TimeSpan.FromSeconds(8);
            options.AttemptTimeout = TimeSpan.FromSeconds(2);
            options.Retry.MaxRetryAttempts = 0;
            options.CircuitBreaker.Enabled = false;
            options.Concurrency.Enabled = false;
            options.SyntheticResponsePolicy = policy;
            options.ResponseObserver = observer;
            foreach (var operation in operations) options.SyntheticOperations.Add(operation);
            options.Hedging.ValidateCustomTransport = handler => handler is SendCounterHandler;
            configure?.Invoke(options);
        });
        if (addReplay)
            builder.AddBingHttpWriteReplay(options =>
            {
                options.Enabled = true;
                options.GetContract = candidate => new HttpIdempotencyContract(candidate.ContractVersion,
                    candidate.IdempotencyScope!, TimeSpan.FromDays(1), SharedAcrossEndpoints: false);
                options.CanReplayWrite = _ => true;
            });
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        return new SyntheticClientHost(provider, client, sends);
    }

    private static ResilienceOperationDescriptor ReadOperation(string name) =>
        new(name, "v1", ResilienceOperationKind.ReadOnly);

    private static HttpRequestMessage ReadRequest(ResilienceOperationDescriptor operation)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://catalog.test/items");
        request.Options.Set(BingHttpOperationMetadata.Operation, operation);
        request.Options.Set(BingHttpOperationMetadata.AccessScope, "tenant-test");
        return request;
    }

    private static HttpRequestMessage WriteRequest(ResilienceOperationDescriptor operation, string key, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://orders.test/orders")
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body))
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        request.Options.Set(BingHttpOperationMetadata.Operation, operation);
        request.Options.Set(BingHttpOperationMetadata.IdempotencyKey, key);
        request.Options.Set(BingHttpOperationMetadata.AccessScope, "tenant-test");
        return request;
    }

    private static HttpSyntheticResponse Cached(HttpSyntheticRequest request, string sourceId, HttpResponseMessage? response = null) =>
        new(response ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("cached") },
            HttpSyntheticResponseKind.Cached, request.Operation.Name, request.Operation.ContractVersion, request.AccessScope,
            sourceId, DateTimeOffset.UtcNow.AddMinutes(5));

    private static IHttpSyntheticResponsePolicy DurableAcceptedPolicy(ProofHolder proof, HttpContent content, bool includeProof = true) =>
        new DelegatePolicy((request, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = content,
                Headers = { Location = new Uri("https://orders.test/operations/" + (proof.Value?.OperationId ?? "missing")) }
            };
            return ValueTask.FromResult<HttpSyntheticResponse?>(new(response, HttpSyntheticResponseKind.DurableAccepted,
                request.Operation.Name, request.Operation.ContractVersion, request.AccessScope, "replay-journal",
                CommitProof: includeProof ? proof.Value : null));
        });

    private sealed class ProofHolder { public DurableOperationReceipt? Value { get; set; } }

    private sealed class DelegatePolicy(
        Func<HttpSyntheticRequest, HttpSyntheticFailure, CancellationToken, ValueTask<HttpSyntheticResponse?>> create)
        : IHttpSyntheticResponsePolicy
    {
        public ValueTask<HttpSyntheticResponse?> TryCreateAsync(HttpSyntheticRequest request, HttpSyntheticFailure failure,
            CancellationToken token) => create(request, failure, token);
    }

    private sealed class RecordingObserver : IHttpResilienceResponseObserver
    {
        private readonly ConcurrentQueue<HttpResponseObservation> _observations = new();
        public IReadOnlyList<HttpResponseObservation> Observations => _observations.ToArray();
        public ValueTask ObserveAsync(HttpResponseObservation response, CancellationToken token)
        {
            _observations.Enqueue(response);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : IHttpResilienceResponseObserver
    {
        public ValueTask ObserveAsync(HttpResponseObservation response, CancellationToken token) =>
            ValueTask.FromException(new InvalidOperationException("diagnostics must not change the response"));
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class SendCounterHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private int _sends;
        public int Sends => Volatile.Read(ref _sends);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sends);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class SyntheticClientHost(ServiceProvider services, HttpClient client, SendCounterHandler transport) : IDisposable
    {
        public ServiceProvider Services { get; } = services;
        public HttpClient Client { get; } = client;
        public int TransportSends => transport.Sends;
        public void Dispose()
        {
            Client.Dispose();
            Services.Dispose();
        }
    }

    private sealed class TrackingContent(string value) : HttpContent
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(value);
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes, 0, _bytes.Length);
        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Interlocked.Increment(ref _disposeCount);
            base.Dispose(disposing);
        }
    }

    private sealed class NoopReplayHandler : IReplayCommandHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
    }
}
