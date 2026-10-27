using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Replay;
using Bing.Resilience;
using Bing.Resilience.Idempotency.AspNetCore;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class HttpWriteReplayTests
{
    private static readonly HttpRequestOptionsKey<List<int>> MutableOption = new("test.mutable");

    [Fact]
    public async Task PermittedPostRetriesWithFreshMessageContentAndSignatureAndCopiedOptions()
    {
        var operation = WriteOperation("orders.post");
        var handlerRecords = new List<CapturedRequest>();
        var signatures = new SigningHandler();
        var responses = 0;
        var handler = new StubTransportHandler(async (request, token) =>
        {
            var body = await request.Content!.ReadAsByteArrayAsync(token);
            request.Options.TryGetValue(MutableOption, out var option);
            handlerRecords.Add(new CapturedRequest(
                request,
                request.Content,
                request.Headers.GetValues("Idempotency-Key").Single(),
                request.Headers.TryGetValues("X-Signature", out var signature) ? signature.Single() : null,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Cookie", out var cookie) ? cookie.Single() : null,
                request.Headers.TryGetValues("X-Trace-Id", out var trace) ? trace.Single() : null,
                body,
                option));
            return new HttpResponseMessage(Interlocked.Increment(ref responses) == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.Created);
        });
        using var host = CreateHttpClient("retry-success", handler, new[] { operation }, signing: signatures,
            configureReplay: options =>
            {
                options.ForwardHeaders.Add("X-Trace-Id");
                options.SignatureHeaders.Add("X-Signature");
                options.OptionCopiers[MutableOption.Key] = value => ((List<int>)value!).ToList();
            });

        var sourceOption = new List<int> { 4, 5 };
        using var request = CreateWriteRequest(operation, "stable-key", "same-body");
        request.Options.Set(MutableOption, sourceOption);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-only");
        request.Headers.TryAddWithoutValidation("Cookie", "session=test-only");
        request.Headers.TryAddWithoutValidation("X-Signature", "caller-value");
        request.Headers.TryAddWithoutValidation("X-Trace-Id", "trace-17");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, handlerRecords.Count);
        Assert.NotSame(handlerRecords[0].Request, handlerRecords[1].Request);
        Assert.NotSame(handlerRecords[0].Content, handlerRecords[1].Content);
        Assert.Equal("stable-key", handlerRecords[0].IdempotencyKey);
        Assert.Equal(handlerRecords[0].IdempotencyKey, handlerRecords[1].IdempotencyKey);
        Assert.All(handlerRecords, record => Assert.Equal(Encoding.UTF8.GetBytes("same-body"), record.Body));
        Assert.Equal(new[] { "signature-1", "signature-2" }, signatures.Values.ToArray());
        Assert.Equal(new[] { "signature-1", "signature-2" }, handlerRecords.Select(record => record.Signature));
        Assert.All(handlerRecords, record => Assert.Null(record.Authorization));
        Assert.All(handlerRecords, record => Assert.Null(record.Cookie));
        Assert.All(handlerRecords, record => Assert.Equal("trace-17", record.Trace));
        Assert.NotNull(handlerRecords[0].MutableOption);
        Assert.NotNull(handlerRecords[1].MutableOption);
        Assert.NotSame(sourceOption, handlerRecords[0].MutableOption);
        Assert.NotSame(handlerRecords[0].MutableOption, handlerRecords[1].MutableOption);
        handlerRecords[0].MutableOption!.Add(99);
        Assert.Equal(new[] { 4, 5 }, handlerRecords[1].MutableOption);
        Assert.Equal(new[] { 4, 5 }, sourceOption);
    }

    [Fact]
    public async Task UnapprovedStreamAndDurableAttemptsAreNotAutomaticallyRepeated()
    {
        var approved = WriteOperation("orders.approved");
        var unapproved = WriteOperation("orders.unapproved");
        var sends = 0;
        var handler = new StubTransportHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        using var host = CreateHttpClient("replay-eligibility", handler, new[] { approved, unapproved },
            configureReplay: options => options.CanReplayWrite = request =>
                request.Options.TryGetValue(BingHttpOperationMetadata.Operation, out var operation) && operation == approved);

        using (var request = CreateWriteRequest(unapproved, "unapproved-key", "body"))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using (var request = CreateWriteRequest(approved, "stream-key", "body"))
        {
            request.Options.Set(BingHttpOperationMetadata.IsStreamResult, true);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        using (var request = CreateWriteRequest(approved, "durable-key", "body"))
        {
            request.Options.Set(BingHttpOperationMetadata.DurableAttempt, true);
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        Assert.Equal(3, sends);
    }

    [Fact]
    public async Task NonReplayableStreamContentIsRejectedBeforeFirstSend()
    {
        var operation = WriteOperation("orders.nonreplayable");
        var sends = 0;
        var handler = new StubTransportHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        using var host = CreateHttpClient("nonreplayable-body", handler, new[] { operation });
        using var request = CreateWriteRequest(operation, "nonreplayable-key", "ignored");
        request.Content?.Dispose();
        request.Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("one-shot")));

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));
        Assert.Equal(0, sends);
    }

    [Fact]
    public void EnablingReplayWithoutAReceivingProtocolFailsAtRegistration()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("missing-protocol");

        Assert.Throws<ResilienceConfigurationException>(() => builder.AddBingHttpWriteReplay(options =>
        {
            options.Enabled = true;
            options.CanReplayWrite = _ => true;
        }));
    }

    [Fact]
    public async Task IncompatibleProtocolAndOversizedBodyFailBeforeTransportSend()
    {
        var operation = WriteOperation("orders.protocol");
        var protocolSends = 0;
        var protocolHandler = new StubTransportHandler((_, _) =>
        {
            Interlocked.Increment(ref protocolSends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using (var host = CreateHttpClient("contract-mismatch", protocolHandler, new[] { operation },
                   configureReplay: options => options.GetContract = _ =>
                       new HttpIdempotencyContract("v2", operation.IdempotencyScope!, TimeSpan.FromDays(1), false)))
        using (var request = CreateWriteRequest(operation, "contract-key", "body"))
        {
            await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));
        }
        Assert.Equal(0, protocolSends);

        var bodySends = 0;
        var bodyHandler = new StubTransportHandler((_, _) =>
        {
            Interlocked.Increment(ref bodySends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using (var host = CreateHttpClient("oversized-body", bodyHandler, new[] { operation }, configureReplay: options =>
               {
                   options.MaxBufferedBytes = 4;
                   options.MaxPayloadBytes = 8;
               }))
        using (var request = CreateWriteRequest(operation, "large-key", "12345"))
        {
            await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));
        }
        Assert.Equal(0, bodySends);
    }

    [Fact]
    public async Task MutableRequestOptionsRequireAnExplicitCopier()
    {
        var operation = WriteOperation("orders.mutable-options");
        var sends = 0;
        var handler = new StubTransportHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var host = CreateHttpClient("mutable-options", handler, new[] { operation });
        using var request = CreateWriteRequest(operation, "mutable-key", "body");
        request.Options.Set(MutableOption, new List<int> { 1 });

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => host.Client.SendAsync(request));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task KestrelRetryAfterCommittedResponseDisconnectDoesNotDuplicateBusinessWrite()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var operation = WriteOperation("orders.kestrel");
        var executions = 0;
        await using var server = await LoopbackHost.StartAsync(database, new[] { new EndpointSpec("/orders", operation) },
            async context =>
            {
                Interlocked.Increment(ref executions);
                await InsertBusinessRowAsync(context, "created-once");
                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsync("order-created");
            }, abortFirstResponseWrite: true);
        using var signatures = new SigningHandler();
        using var client = CreateHttpClient("kestrel-replay", CreateSocketsHandler(), new[] { operation }, signing: signatures,
            configureReplay: options => options.ForwardHeaders.Add("X-Trace-Id"));

        using var request = CreateWriteRequest(operation, "kestrel-key", "loopback-payload", server.Origin);
        request.Headers.TryAddWithoutValidation("X-Trace-Id", "loopback-trace");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-only");
        using var response = await client.Client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("order-created", responseBody);
        Assert.Equal(1, executions);
        Assert.Equal(1, server.AbortedResponses);
        Assert.Equal(2, server.Requests.Count);
        Assert.All(server.Requests, captured => Assert.Equal("kestrel-key", captured.IdempotencyKey));
        Assert.All(server.Requests, captured => Assert.Equal(Encoding.UTF8.GetBytes("loopback-payload"), captured.Body));
        Assert.All(server.Requests, captured => Assert.Equal("loopback-trace", captured.Trace));
        Assert.All(server.Requests, captured => Assert.Null(captured.Authorization));
        Assert.Equal(new[] { "signature-1", "signature-2" }, server.Requests.Select(captured => captured.Signature));
        Assert.Equal(2, signatures.Values.Count);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal(1, await database.CountIdempotencyRowsAsync());
    }

    [Fact]
    public async Task KestrelRejectsSameKeyWithDifferentBodyAsConflict()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var operation = WriteOperation("orders.conflict-loopback");
        var executions = 0;
        await using var server = await LoopbackHost.StartAsync(database, new[] { new EndpointSpec("/orders", operation) },
            async context =>
            {
                Interlocked.Increment(ref executions);
                await InsertBusinessRowAsync(context, "first-body");
                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsync("saved");
            });
        using var client = CreateHttpClient("kestrel-conflict", CreateSocketsHandler(), new[] { operation }, maxRetryAttempts: 0);

        using (var first = CreateWriteRequest(operation, "same-key", "first-body", server.Origin))
        using (var response = await client.Client.SendAsync(first))
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var second = CreateWriteRequest(operation, "same-key", "changed-body", server.Origin))
        using (var response = await client.Client.SendAsync(second))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        Assert.Equal(1, executions);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal(1, await database.CountIdempotencyRowsAsync());
    }

    [Fact]
    public async Task KestrelNonSuccessAndEndpointExceptionsRollBackJournalAndBusinessWrites()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var badOperation = WriteOperation("orders.non-success");
        var exceptionOperation = WriteOperation("orders.exception");
        var badCalls = 0;
        var exceptionCalls = 0;
        await using var server = await LoopbackHost.StartAsync(database,
            new[] { new EndpointSpec("/non-success", badOperation), new EndpointSpec("/exception", exceptionOperation) },
            async context =>
            {
                var tx = Assert.IsAssignableFrom<IRelationalOperationTransaction>(
                    context.Features.Get<IOperationTransactionFeature>()!.Transaction);
                if (context.Request.Path == "/non-success")
                {
                    var call = Interlocked.Increment(ref badCalls);
                    await InsertBusinessRowAsync(tx, "non-success");
                    context.Response.StatusCode = call == 1 ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status201Created;
                    await context.Response.WriteAsync(call == 1 ? "rejected" : "accepted");
                    return;
                }

                Interlocked.Increment(ref exceptionCalls);
                await InsertBusinessRowAsync(tx, "exception");
                throw new InvalidOperationException("endpoint failure");
            });
        using var client = CreateHttpClient("kestrel-failures", CreateSocketsHandler(), new[] { badOperation, exceptionOperation }, maxRetryAttempts: 0);

        using (var first = CreateWriteRequest(badOperation, "bad-key", "same-body", server.Origin, "/non-success"))
        using (var response = await client.Client.SendAsync(first))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await database.CountBusinessRowsAsync());
        Assert.Equal(0, await database.CountIdempotencyRowsAsync());

        using (var second = CreateWriteRequest(badOperation, "bad-key", "same-body", server.Origin, "/non-success"))
        using (var response = await client.Client.SendAsync(second))
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, badCalls);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal(1, await database.CountIdempotencyRowsAsync());

        using (var first = CreateWriteRequest(exceptionOperation, "exception-key", "exception-body", server.Origin, "/exception"))
        using (var response = await client.Client.SendAsync(first))
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal(1, await database.CountIdempotencyRowsAsync());

        using (var second = CreateWriteRequest(exceptionOperation, "exception-key", "exception-body", server.Origin, "/exception"))
        using (var response = await client.Client.SendAsync(second))
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, exceptionCalls);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal(1, await database.CountIdempotencyRowsAsync());
    }

    [Fact]
    public async Task KestrelMiddlewareRejectsOversizedRequestBody()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var operation = WriteOperation("orders.request-limit");
        var executions = 0;
        await using var server = await LoopbackHost.StartAsync(database, new[] { new EndpointSpec("/limited", operation) },
            context =>
            {
                Interlocked.Increment(ref executions);
                return Task.CompletedTask;
            }, maxRequestBytes: 4);
        using var client = CreateHttpClient("kestrel-request-limit", CreateSocketsHandler(), new[] { operation }, maxRetryAttempts: 0);
        using var request = CreateWriteRequest(operation, "limit-key", "12345", server.Origin, path: "/limited");

        using var response = await client.Client.SendAsync(request);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, (int)response.StatusCode);
        Assert.Equal(0, executions);
        Assert.Equal(0, await database.CountBusinessRowsAsync());
        Assert.Equal(0, await database.CountIdempotencyRowsAsync());
    }

    private static ResilienceOperationDescriptor WriteOperation(string name) =>
        new(name, "v1", ResilienceOperationKind.IdempotentWrite, "orders-shared");

    private static HttpRequestMessage CreateWriteRequest(ResilienceOperationDescriptor operation, string key, string body,
        Uri? origin = null, string path = "/orders")
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            origin is null ? new Uri("https://service.test" + path) : new Uri(origin, path));
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        request.Options.Set(BingHttpOperationMetadata.Operation, operation);
        request.Options.Set(BingHttpOperationMetadata.IdempotencyKey, key);
        request.Options.Set(BingHttpOperationMetadata.AccessScope, "tenant-test");
        return request;
    }

    private static ClientHost CreateHttpClient(string name, HttpMessageHandler primary,
        IReadOnlyCollection<ResilienceOperationDescriptor> operations, int maxRetryAttempts = 1,
        SigningHandler? signing = null, Action<HttpWriteReplayOptions>? configureReplay = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBingResilience().AddFencesProvider("fences").UseDefaultProvider("fences");
        services.AddBingResilienceOperations(operations.ToArray());
        var builder = services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => primary);
        builder.AddBingHttpResilience("http-write-replay", options =>
        {
            options.RequestTimeout = TimeSpan.FromSeconds(10);
            options.AttemptTimeout = TimeSpan.FromSeconds(3);
            options.Retry.MaxRetryAttempts = maxRetryAttempts;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.MaxDelay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
            options.CircuitBreaker.Enabled = false;
            options.Concurrency.Enabled = false;
            options.Hedging.ValidateCustomTransport = handler => handler is StubTransportHandler;
        });
        if (signing is not null) builder.AddHttpMessageHandler(() => signing);
        builder.AddBingHttpWriteReplay(options =>
        {
            options.Enabled = true;
            options.GetContract = operation => new HttpIdempotencyContract(
                operation.ContractVersion, operation.IdempotencyScope!, TimeSpan.FromDays(1), SharedAcrossEndpoints: false);
            options.CanReplayWrite = _ => true;
            configureReplay?.Invoke(options);
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        return new ClientHost(provider, client);
    }

    private static SocketsHttpHandler CreateSocketsHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };

    private static async Task InsertBusinessRowAsync(HttpContext context, string value)
    {
        var transaction = Assert.IsAssignableFrom<IRelationalOperationTransaction>(
            context.Features.Get<IOperationTransactionFeature>()!.Transaction);
        await InsertBusinessRowAsync(transaction, value);
    }

    private static async Task InsertBusinessRowAsync(IRelationalOperationTransaction transaction, string value)
    {
        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction.Transaction;
        command.CommandText = "INSERT INTO business_rows (value) VALUES ($value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$value";
        parameter.Value = value;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record CapturedRequest(HttpRequestMessage Request, HttpContent? Content, string IdempotencyKey,
        string? Signature, string? Authorization, string? Cookie, string? Trace, byte[] Body, List<int>? MutableOption);

    private sealed class StubTransportHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class SigningHandler : DelegatingHandler
    {
        private int _sequence;
        private readonly ConcurrentQueue<string> _values = new();
        public IReadOnlyCollection<string> Values => _values.ToArray();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var signature = "signature-" + Interlocked.Increment(ref _sequence);
            _values.Enqueue(signature);
            request.Headers.TryAddWithoutValidation("X-Signature", signature);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ClientHost(ServiceProvider services, HttpClient client) : IDisposable
    {
        public HttpClient Client { get; } = client;
        public void Dispose()
        {
            Client.Dispose();
            services.Dispose();
        }
    }

    private sealed record EndpointSpec(string Path, ResilienceOperationDescriptor Operation);
    private sealed record ServerRequest(string IdempotencyKey, byte[] Body, string? Signature, string? Authorization, string? Trace);

    private sealed class LoopbackHost : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private readonly AtomicCounter _abortedResponses;

        private LoopbackHost(WebApplication application, Uri origin, ConcurrentQueue<ServerRequest> requests, AtomicCounter abortedResponses)
        {
            _application = application;
            Origin = origin;
            Requests = requests;
            _abortedResponses = abortedResponses;
        }

        public Uri Origin { get; }
        public ConcurrentQueue<ServerRequest> Requests { get; }
        public int AbortedResponses => Volatile.Read(ref _abortedResponses.Value);

        public static async Task<LoopbackHost> StartAsync(SqliteDatabase database, IReadOnlyCollection<EndpointSpec> endpoints,
            RequestDelegate endpointHandler, bool abortFirstResponseWrite = false, int maxRequestBytes = 1024 * 1024)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton<IResilienceOperationStore>(database.Store);
            builder.Services.AddBingResilienceOperations(endpoints.Select(x => x.Operation).Distinct().ToArray());
            var app = builder.Build();
            var requestCaptures = new ConcurrentQueue<ServerRequest>();
            var abortedResponses = new AtomicCounter();
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body, context.RequestAborted);
                context.Request.Body.Position = 0;
                requestCaptures.Enqueue(new ServerRequest(
                    context.Request.Headers["Idempotency-Key"].SingleOrDefault() ?? "",
                    body.ToArray(),
                    context.Request.Headers["X-Signature"].SingleOrDefault(),
                    context.Request.Headers["Authorization"].SingleOrDefault(),
                    context.Request.Headers["X-Trace-Id"].SingleOrDefault()));

                if (abortFirstResponseWrite && context.Request.Path == endpoints.First().Path &&
                    Interlocked.CompareExchange(ref abortedResponses.Value, 1, 0) == 0)
                    context.Response.Body = new AbortFirstWriteStream(context.Response.Body, context);

                await next(context);
            });
            app.UseRouting();
            var endpointMap = endpoints.ToDictionary(x => x.Path, x => x, StringComparer.Ordinal);
            app.UseBingIdempotency(options =>
            {
                options.Retention = TimeSpan.FromDays(1);
                options.MaxRequestBytes = maxRequestBytes;
                options.ResolveEndpoint = context => endpointMap.TryGetValue(context.Request.Path.Value ?? "", out var endpoint)
                    ? new IdempotentEndpoint(endpoint.Operation, "tenant-test")
                    : null;
            });
            foreach (var endpoint in endpoints) app.MapPost(endpoint.Path, endpointHandler);

            try
            {
                await app.StartAsync();
                var server = app.Services.GetRequiredService<IServer>();
                var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault()
                    ?? throw new InvalidOperationException("Kestrel did not publish its loopback address.");
                var origin = new Uri(address);
                if (origin.Port == 0) throw new InvalidOperationException("Kestrel did not resolve its ephemeral loopback port.");
                return new LoopbackHost(app, origin, requestCaptures, abortedResponses);
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() => await _application.DisposeAsync();
    }

    private sealed class AtomicCounter
    {
        public int Value;
    }

    private sealed class AbortFirstWriteStream(Stream inner, HttpContext context) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => Abort();
        public override void Write(ReadOnlySpan<byte> buffer) => Abort();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => FailAsync();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => FailValueTaskAsync();

        private Task FailAsync()
        {
            Abort();
            return Task.CompletedTask;
        }

        private ValueTask FailValueTaskAsync()
        {
            Abort();
            return ValueTask.CompletedTask;
        }

        private void Abort()
        {
            context.Abort();
            throw new IOException("Loopback test aborted the first successful response after commit.");
        }
    }

    private sealed class SqliteDatabase : IAsyncDisposable
    {
        private SqliteDatabase(string path, string connectionString, RelationalOperationStore store)
        {
            Path = path;
            ConnectionString = connectionString;
            Store = store;
        }

        private string Path { get; }
        private string ConnectionString { get; }
        public RelationalOperationStore Store { get; }

        public static async Task<SqliteDatabase> CreateAsync()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bing-http-write-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = 5
            }.ToString();
            var store = new RelationalOperationStore(
                _ => ValueTask.FromResult<DbConnection>(new SqliteConnection(connectionString)),
                ResilienceRelationalDialect.Sqlite,
                tablePrefix: "journal");
            var database = new SqliteDatabase(path, connectionString, store);
            await store.InitializeAsync();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE business_rows (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL)";
            await command.ExecuteNonQueryAsync();
            return database;
        }

        public async Task<int> CountBusinessRowsAsync() => await CountAsync("SELECT COUNT(*) FROM business_rows");
        public async Task<int> CountIdempotencyRowsAsync() => await CountAsync("SELECT COUNT(*) FROM journal_operations WHERE kind='idempotency'");

        private async Task<int> CountAsync(string sql)
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path)) File.Delete(Path);
            return ValueTask.CompletedTask;
        }
    }
}
