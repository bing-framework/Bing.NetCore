using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Bing.Application.Services;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.Contracts;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Discovery;
using Bing.Http.Resilience.Replay;
using Bing.Resilience.Discovery;
using Bing.Resilience.Operations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebApiClientCore.Attributes;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed record ExtensionReply(string Value);
public interface IExtensionRuntime : IAppService
{
    Task<ExtensionReply> ReadAsync(CancellationToken cancellationToken = default);
    Task<ExtensionReply> WriteAsync(CancellationToken cancellationToken = default);
}
public interface IExtensionSdk
{
    [HttpGet("read")][JsonReturn] Task<ExtensionReply> ReadAsync(CancellationToken cancellationToken = default);
    [HttpPost("write")][JsonReturn] Task<ExtensionReply> WriteAsync([JsonContent] ExtensionReply body, CancellationToken cancellationToken = default);
}

/// <summary>真实 Kestrel、SDK/运行时代理与恢复 handler 的组合验证。</summary>
public sealed class ClientExtensionsLoopbackTests
{
    private static readonly ResilienceOperationDescriptor ReadOperation = new("catalog.read", "v1", ResilienceOperationKind.ReadOnly);
    private static readonly ResilienceOperationDescriptor WriteOperation = new("catalog.write", "v1", ResilienceOperationKind.IdempotentWrite, "catalog");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryAndSwitchAffectCachedClientAndRefreshEverySignature(bool runtime)
    {
        var calls = new ConcurrentQueue<string>();
        var recovered = false;
        await using var primary = await Server.Start(async context =>
        {
            if (IsDefinition(context)) { await context.Response.WriteAsJsonAsync(Definition()); return; }
            calls.Enqueue("primary:" + context.Request.Headers["X-Signature"]);
            if (!recovered) { context.Response.StatusCode = 503; return; }
            await context.Response.WriteAsJsonAsync(new ExtensionReply("primary"));
        });
        await using var backup = await Server.Start(async context =>
        {
            calls.Enqueue("backup:" + context.Request.Headers["X-Signature"]);
            await context.Response.WriteAsJsonAsync(new ExtensionReply("backup"));
        });
        var source = new ConfigurationServiceEndpointSource(
            new DelegateServiceEndpointTrustPolicy((_, endpoint) => endpoint.Origin == primary.Address || endpoint.Origin == backup.Address), TimeProvider.System);
        source.Publish(Snapshot("a", primary.Address, backup.Address), null);
        var services = BaseServices();
        services.AddSingleton<IServiceEndpointSource>(source);
        // 本用例刻意制造主端点失败，固定主备顺序；默认加权分布由 WeightedDiscoveryTests 验证。
        services.AddSingleton<IServiceEndpointSelector, OrderedServiceEndpointSelector>();
        var builder = Register(services, runtime, primary.Address, o =>
        {
            o.UseDynamicRouting = true; o.CanRetryRequest = _ => true;
        });
        builder.AddBingHttpDiscovery(o =>
        {
            o.ServiceName = "catalog"; o.LogicalOrigin = primary.Address;
            o.ContractRevision = "v1"; o.ConsistencyGroup = "stable"; o.CredentialAudience = "catalog";
        }).AddHttpMessageHandler(() => new Signer());
        await using var host = services.BuildServiceProvider();
        var api = Client(host, runtime);
        Assert.Equal("backup", (await api.Read(default)).Value);
        recovered = true;
        source.Publish(Snapshot("b", primary.Address), "a");
        var manager = host.GetRequiredService<IResilienceRuntimeManager>();
        var current = manager.GetSnapshot("http:" + builder.Name);
        await manager.SwitchAsync("http:" + builder.Name, "fences", current.Version);
        Assert.Equal("primary", (await api.Read(default)).Value);
        Assert.Equal(3, calls.Count);
        Assert.Equal(3, calls.Select(x => x.Split(':')[1]).Distinct().Count());
        Assert.Equal(new[] { "primary", "backup", "primary" }, calls.Select(x => x.Split(':')[0]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptedInWriteUsesSameKeyAndFreshSignatureThroughRealClient(bool runtime)
    {
        var requests = new ConcurrentQueue<(string Key, string Signature, string Body)>();
        await using var server = await Server.Start(async context =>
        {
            if (IsDefinition(context)) { await context.Response.WriteAsJsonAsync(Definition()); return; }
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            requests.Enqueue((context.Request.Headers["Idempotency-Key"].ToString(), context.Request.Headers["X-Signature"].ToString(),
                await reader.ReadToEndAsync()));
            if (requests.Count == 1) { context.Response.StatusCode = 503; return; }
            await context.Response.WriteAsJsonAsync(new ExtensionReply("created"));
        });
        var services = BaseServices();
        services.AddBingResilienceOperations(WriteOperation);
        var metadata = new Metadata();
        var builder = Register(services, runtime, server.Address, _ => { }, metadata);
        builder.AddBingHttpWriteReplay(o =>
        {
            o.Enabled = true; o.CanReplayWrite = _ => true;
            o.CanBufferContent = _ => true;
            o.GetContract = _ => new("v1", "catalog", TimeSpan.FromHours(1), true);
            o.OptionCopiers["Bing.DynamicApi.RequestMetadata"] = value => value;
        }).AddHttpMessageHandler(() => new Signer());
        await using var host = services.BuildServiceProvider();
        Assert.Equal("created", (await Client(host, runtime).Write(default)).Value);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("client-operation-1", request.Key));
        Assert.Single(requests.Select(request => request.Body).Distinct());
        Assert.Equal(2, requests.Select(request => request.Signature).Distinct().Count());
        Assert.Equal(1, metadata.BusinessCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticDtoPreservesClientResultAndDoesNotHideCircuitFailures(bool runtime)
    {
        var networkCalls = 0;
        await using var server = await Server.Start(async context =>
        {
            if (IsDefinition(context)) { await context.Response.WriteAsJsonAsync(Definition()); return; }
            Interlocked.Increment(ref networkCalls);
            context.Response.StatusCode = 503;
        });
        var observer = new Observer();
        var services = BaseServices();
        Register(services, runtime, server.Address, o =>
        {
            o.Retry.MaxRetryAttempts = 0; o.CircuitBreaker.Enabled = true; o.CircuitBreaker.MinimumThroughput = 2;
            o.CircuitBreaker.FailureRatio = 0.5; o.SyntheticOperations.Add(ReadOperation);
            o.SyntheticResponsePolicy = new CachePolicy(); o.ResponseObserver = observer;
        }, new Metadata());
        await using var host = services.BuildServiceProvider();
        var api = Client(host, runtime);
        for (var i = 0; i < 3; i++) Assert.Equal("cached", (await api.Read(default)).Value);
        // 运行时旧描述路径的一次成功也属于该实际 client 的熔断采样。
        Assert.Equal(runtime ? 1 : 2, networkCalls);
        Assert.Equal(3, observer.Values.Count(x => x.IsSynthetic));
        Assert.All(observer.Values.Where(x => x.IsSynthetic), x => Assert.Equal("catalog-cache", x.SourceId));
    }

    [Fact]
    public async Task RuntimeDefinitionRacesFullBodiesCachesWinnerAndKeepsWaiterCancellationIsolated()
    {
        var primaryDefinitions = 0; var backupDefinitions = 0; var invocations = 0;
        var primaryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backupRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var primary = await Server.Start(async context =>
        {
            if (!IsDefinition(context))
            {
                Interlocked.Increment(ref invocations);
                await context.Response.WriteAsJsonAsync(new ExtensionReply("real"));
                return;
            }
            Interlocked.Increment(ref primaryDefinitions);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{");
            await context.Response.Body.FlushAsync();
            primaryStarted.TrySetResult(true);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted); }
            catch (OperationCanceledException) { primaryCancelled.TrySetResult(true); }
        });
        await using var backup = await Server.Start(async context =>
        {
            Interlocked.Increment(ref backupDefinitions);
            await backupRelease.Task.WaitAsync(context.RequestAborted);
            await context.Response.WriteAsJsonAsync(Definition());
        });
        var services = BaseServices();
        services.AddBingDynamicApiClient(o => o.AddRemoteService("catalog", r => r.BaseUrl = primary.Address.ToString()));
        services.AddBingDynamicApiClient<IExtensionRuntime>("CATALOG");
        services.AddBingDynamicApiClientResilience("catalog", o =>
        {
            o.DefinitionTimeout = TimeSpan.FromSeconds(5);
            o.Definition.Enabled = true; o.Definition.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.Hedging;
            o.Definition.ServiceName = "catalog"; o.Definition.ContractRevision = "v1"; o.Definition.CompatibilityGroup = "stable";
            o.Definition.Delay = TimeSpan.FromMilliseconds(40);
            o.Definition.Endpoints.Add(new("primary", new Uri(primary.Address, "api/dynamic-api/v1/definition")));
            o.Definition.Endpoints.Add(new("backup", new Uri(backup.Address, "api/dynamic-api/v1/definition")));
            o.Definition.TrustEndpoint = uri => uri.IsLoopback;
            o.Definition.CaptureAccess = _ => new("tenant-a", "catalog", new Dictionary<string, string>());
            o.Definition.ValidateIdentity = (_, _) => true;
        });
        await using var host = services.BuildServiceProvider();
        var api = host.GetRequiredService<IExtensionRuntime>();
        using var cancel = new CancellationTokenSource();
        var first = api.ReadAsync(cancel.Token);
        await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = api.ReadAsync();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        backupRelease.SetResult(true);
        Assert.Equal("real", (await second).Value);
        await primaryCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("real", (await api.ReadAsync()).Value);
        Assert.Equal(1, primaryDefinitions);
        Assert.Equal(1, backupDefinitions);
        Assert.Equal(2, invocations);
    }

    private static ServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBingResilience().AddBuiltInProvider().AddFencesProvider("fences").UseDefaultProvider("builtin").AddRuntimeSwitching();
        return services;
    }
    private static IHttpClientBuilder Register(ServiceCollection services, bool runtime, Uri origin, Action<BingHttpResilienceOptions> configure, Metadata? metadata = null)
    {
        void Http(BingHttpResilienceOptions o)
        {
            o.Retry.Delay = TimeSpan.Zero; o.Retry.UseJitter = false; o.Retry.MaxRetryAttempts = 1;
            o.CircuitBreaker.Enabled = false; configure(o);
        }
        IHttpClientBuilder builder;
        if (runtime)
        {
            services.AddBingDynamicApiClient(o => o.AddRemoteService("catalog", r => r.BaseUrl = origin.ToString()));
            services.AddBingDynamicApiClient<IExtensionRuntime>("CATALOG");
            if (metadata is not null) services.AddSingleton<IBingDynamicApiRequestHandler>(metadata);
            builder = services.AddBingDynamicApiClientResilience("catalog", o => Http(o.Http));
        }
        else
        {
            builder = services.AddBingWebApiClient<IExtensionSdk>(o => o.BaseAddress = origin);
            if (metadata is not null) builder.AddHttpMessageHandler(() => new MetadataHandler(metadata));
            builder.AddBingHttpResilience("sdk", Http);
        }
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        return builder;
    }
    private static (Func<CancellationToken, Task<ExtensionReply>> Read, Func<CancellationToken, Task<ExtensionReply>> Write) Client(IServiceProvider provider, bool runtime)
    {
        if (runtime)
        {
            var api = provider.GetRequiredService<IExtensionRuntime>();
            return (api.ReadAsync, api.WriteAsync);
        }
        var sdk = provider.GetRequiredService<IExtensionSdk>();
        return (sdk.ReadAsync, token => sdk.WriteAsync(new("body"), token));
    }
    private static ServiceEndpointSnapshot Snapshot(string version, params Uri[] addresses)
    {
        var now = DateTimeOffset.UtcNow;
        return new("catalog", version, now, now.AddMinutes(1), now.AddMinutes(2),
            addresses.Select((address, index) => new ServiceEndpoint("node-" + index, "1", address, "v1", "stable", "catalog")).ToArray());
    }
    private static bool IsDefinition(HttpContext context) => context.Request.Path.Value!.EndsWith("/definition", StringComparison.Ordinal);
    private static BingDynamicApiDefinition Definition() => new()
    {
        ApiVersion = "v1", Services = { new()
        {
            ServiceName = "catalog", ServiceTypeName = typeof(IExtensionRuntime).FullName,
            Methods = { Method("ReadAsync", "GET", "read"), Method("WriteAsync", "POST", "write") }
        } }
    };
    private static BingDynamicApiMethodDefinition Method(string name, string verb, string path) => new()
    {
        MethodName = name, HttpMethod = verb, RouteTemplate = path, ReturnTypeName = typeof(ExtensionReply).FullName,
        Parameters = { new() { Name = "cancellationToken", Position = 0, TypeName = typeof(CancellationToken).FullName,
            BindingSource = "Special", IsCancellationToken = true } }
    };
    private sealed class Metadata : IBingDynamicApiRequestHandler
    {
        internal int BusinessCalls;
        public ValueTask HandleAsync(string name, HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/definition", StringComparison.Ordinal)) return default;
            Interlocked.Increment(ref BusinessCalls);
            request.Options.Set(BingHttpOperationMetadata.Operation, request.Method == HttpMethod.Post ? WriteOperation : ReadOperation);
            request.Options.Set(BingHttpOperationMetadata.AccessScope, "tenant-a");
            if (request.Method == HttpMethod.Post)
            {
                request.Options.Set(BingHttpOperationMetadata.IdempotencyKey, "client-operation-1");
                request.Content ??= new StringContent("{\"value\":\"body\"}", Encoding.UTF8, "application/json");
            }
            return default;
        }
    }
    private sealed class MetadataHandler(Metadata metadata) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { await metadata.HandleAsync("catalog", request, token); return await base.SendAsync(request, token); }
    }
    private sealed class Signer : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            request.Headers.Remove("X-Signature"); request.Headers.Add("X-Signature", Guid.NewGuid().ToString("N"));
            return base.SendAsync(request, token);
        }
    }
    private sealed class Observer : IHttpResilienceResponseObserver
    {
        internal ConcurrentQueue<HttpResponseObservation> Values { get; } = new();
        public ValueTask ObserveAsync(HttpResponseObservation response, CancellationToken token) { Values.Enqueue(response); return default; }
    }
    private sealed class CachePolicy : IHttpSyntheticResponsePolicy
    {
        public ValueTask<HttpSyntheticResponse?> TryCreateAsync(HttpSyntheticRequest request, HttpSyntheticFailure failure, CancellationToken token) =>
            ValueTask.FromResult<HttpSyntheticResponse?>(new(new(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(new ExtensionReply("cached")), Encoding.UTF8, "application/json") },
                HttpSyntheticResponseKind.Cached, ReadOperation.Name, "v1", request.AccessScope, "catalog-cache", DateTimeOffset.UtcNow.AddMinutes(1)));
    }
    private sealed class Server(WebApplication app, Uri address) : IAsyncDisposable
    {
        internal Uri Address { get; } = address;
        internal static async Task<Server> Start(RequestDelegate handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            var app = builder.Build(); app.Run(handler); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new Uri(address + "/"));
        }
        public async ValueTask DisposeAsync() { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await app.StopAsync(stop.Token); await app.DisposeAsync(); }
    }
}
