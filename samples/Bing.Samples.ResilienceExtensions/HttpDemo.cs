using System.Data.Common;
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
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Idempotency.AspNetCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using WebApiClientCore.Attributes;

public sealed record ExtensionDemoDto(string Value);
public interface IExtensionDemoRuntime : IAppService
{
    Task<ExtensionDemoDto> ReadAsync(CancellationToken cancellationToken = default);
    Task<ExtensionDemoDto> WriteAsync(CancellationToken cancellationToken = default);
}
public interface IExtensionDemoSdk
{
    [HttpGet("read")][JsonReturn] Task<ExtensionDemoDto> ReadAsync();
    [HttpGet("cached")][JsonReturn] Task<ExtensionDemoDto> CachedAsync();
    [HttpPost("write")][JsonReturn] Task<ExtensionDemoDto> WriteAsync([JsonContent] ExtensionDemoDto input);
}

/// <summary>能力2/3/7/8：两个真实客户端、动态端点、幂等写重放、缓存成功与完整定义竞速。</summary>
internal static class HttpDemo
{
    private static readonly ResilienceOperationDescriptor Read = new("demo.read", "v1", ResilienceOperationKind.ReadOnly);
    private static readonly ResilienceOperationDescriptor Write = new("demo.write", "v1", ResilienceOperationKind.IdempotentWrite, "demo");
    public static async Task Run()
    {
        var database = Path.Combine(Path.GetTempPath(), "bing-http-extension-" + Guid.NewGuid().ToString("N") + ".db");
        var connection = new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString();
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new SqliteConnection(connection)), ResilienceRelationalDialect.Sqlite);
        await store.InitializeAsync();
        try
        {
            await using var primary = await StartServer(true, store);
            await using var backup = await StartServer(false, store);
            Uri Address(WebApplication app) => new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/");
            var main = Address(primary); var alternate = Address(backup);
            var services = new ServiceCollection(); services.AddLogging();
            services.AddBingResilience().AddBuiltInProvider().UseDefaultProvider("builtin");
            services.AddBingResilienceOperations(Write);
            var source = new ConfigurationServiceEndpointSource(
                new DelegateServiceEndpointTrustPolicy((_, e) => e.Origin == main || e.Origin == alternate), TimeProvider.System);
            var now = DateTimeOffset.UtcNow;
            source.Publish(new("demo", "1", now, now.AddMinutes(5), now.AddMinutes(5),
                new[] { new ServiceEndpoint("main", "1", main, "v1", "stable", "demo"), new ServiceEndpoint("backup", "1", alternate, "v1", "stable", "demo") }), null);
            services.AddSingleton<IServiceEndpointSource>(source);
            var sdk = services.AddBingWebApiClient<IExtensionDemoSdk>(o => o.BaseAddress = main);
            sdk.AddHttpMessageHandler(() => new MetadataHandler("sdk"));
            sdk.AddBingHttpResilience("demo", Configure);
            Complete(sdk);
            services.AddBingDynamicApiClient(o => o.AddRemoteService("demo", r => r.BaseUrl = main.ToString()));
            services.AddBingDynamicApiClient<IExtensionDemoRuntime>("demo");
            services.AddSingleton<IBingDynamicApiRequestHandler>(new RuntimeMetadata());
            var runtime = services.AddBingDynamicApiClientResilience("demo", o =>
            {
                Configure(o.Http);
                o.Definition.Enabled = true; o.Definition.RecoveryMode = BingDynamicApiDefinitionRecoveryMode.Hedging;
                o.Definition.ServiceName = "demo"; o.Definition.ContractRevision = "v1"; o.Definition.CompatibilityGroup = "stable";
                o.Definition.Delay = TimeSpan.FromMilliseconds(30);
                o.Definition.Endpoints.Add(new("main", new Uri(main, "api/dynamic-api/v1/definition")));
                o.Definition.Endpoints.Add(new("backup", new Uri(alternate, "api/dynamic-api/v1/definition")));
                o.Definition.CaptureAccess = _ => new("demo-tenant", "demo", new Dictionary<string, string>());
                o.Definition.TrustEndpoint = uri => uri.IsLoopback;
                o.Definition.ValidateIdentity = (response, _) => response.Headers.TryGetValues("X-Demo-Contract", out var values) && values.Single() == "v1";
            });
            Complete(runtime);
            await using var host = services.BuildServiceProvider();
            var sdkApi = host.GetRequiredService<IExtensionDemoSdk>();
            var runtimeApi = host.GetRequiredService<IExtensionDemoRuntime>();
            var sdkRead = await sdkApi.ReadAsync();
            var runtimeRead = await runtimeApi.ReadAsync();
            var sdkWrite = await sdkApi.WriteAsync(new("payload"));
            var runtimeWrite = await runtimeApi.WriteAsync();
            var cached = await sdkApi.CachedAsync();
            if (sdkRead.Value != "backup" || runtimeRead.Value != "backup" || sdkWrite.Value != "committed" ||
                runtimeWrite.Value != "committed" || cached.Value != "cached") throw new InvalidOperationException();
            Console.WriteLine("SDK/Runtime：发现切换到备用，写入经同键协议提交；缓存来源由观察器报告；定义只缓存完整有效正文。");

            void Complete(IHttpClientBuilder builder)
            {
                builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
                builder.AddBingHttpDiscovery(o =>
                { o.ServiceName = "demo"; o.LogicalOrigin = main; o.ContractRevision = "v1"; o.ConsistencyGroup = "stable"; o.CredentialAudience = "demo"; });
                builder.AddBingHttpWriteReplay(o =>
                {
                    o.Enabled = true; o.CanReplayWrite = _ => true; o.CanBufferContent = _ => true;
                    o.GetContract = _ => new("v1", "demo", TimeSpan.FromDays(1), true);
                    o.OptionCopiers["Bing.DynamicApi.RequestMetadata"] = value => value;
                });
                builder.AddHttpMessageHandler(() => new Signer());
            }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(database + suffix)) File.Delete(database + suffix); }
    }
    private static void Configure(BingHttpResilienceOptions o)
    {
        o.UseDynamicRouting = true; o.CanRetryRequest = _ => true; o.Retry.MaxRetryAttempts = 1;
        o.Retry.Delay = TimeSpan.Zero; o.Retry.UseJitter = false; o.CircuitBreaker.Enabled = false;
        o.SyntheticOperations.Add(Read); o.SyntheticResponsePolicy = new Cache(); o.ResponseObserver = new Observer();
    }
    private static async Task<WebApplication> StartServer(bool primary, RelationalOperationStore store)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBingResilienceOperations(Write);
        builder.Services.AddSingleton<IResilienceOperationStore>(store);
        var app = builder.Build();
        app.UseBingIdempotency(o => o.ResolveEndpoint = context => context.Request.Method == "POST" ? new(Write, "demo-tenant") : null);
        app.Run(async context =>
        {
            if (context.Request.Path.Value!.EndsWith("/definition", StringComparison.Ordinal))
            {
                context.Response.Headers["X-Demo-Contract"] = "v1";
                if (!primary) { await context.Response.WriteAsJsonAsync(Definition()); return; }
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{"); await context.Response.Body.FlushAsync();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted); } catch (OperationCanceledException) { }
                return;
            }
            if (primary || context.Request.Path == "/cached") { context.Response.StatusCode = 503; return; }
            if (context.Request.Method == "POST")
            {
                // 真实业务必须复用此事务写数据库；本例记录在幂等 Journal 中的返回值就是提交结果。
                _ = context.Features.Get<IOperationTransactionFeature>()!.Transaction;
                await context.Response.WriteAsJsonAsync(new ExtensionDemoDto("committed"));
            }
            else await context.Response.WriteAsJsonAsync(new ExtensionDemoDto("backup"));
        });
        await app.StartAsync(); return app;
    }
    private static BingDynamicApiDefinition Definition() => new()
    {
        ApiVersion = "v1", Services = { new() { ServiceName = "demo", ServiceTypeName = typeof(IExtensionDemoRuntime).FullName,
            Methods = { Method("ReadAsync", "GET", "read"), Method("WriteAsync", "POST", "write") } } }
    };
    private static BingDynamicApiMethodDefinition Method(string name, string verb, string route) => new()
    {
        MethodName = name, HttpMethod = verb, RouteTemplate = route, ReturnTypeName = typeof(ExtensionDemoDto).FullName,
        Parameters = { new() { Name = "cancellationToken", Position = 0, TypeName = typeof(CancellationToken).FullName,
            BindingSource = "Special", IsCancellationToken = true } }
    };
    private static void Metadata(string caller, HttpRequestMessage request)
    {
        request.Options.Set(BingHttpOperationMetadata.Operation, request.Method == HttpMethod.Post ? Write : Read);
        request.Options.Set(BingHttpOperationMetadata.AccessScope, "demo-tenant");
        if (request.Method == HttpMethod.Post)
        {
            // 真实调用方在首次提交前生成并持久保留操作键。
            request.Options.Set(BingHttpOperationMetadata.IdempotencyKey, caller + "-operation-1");
            request.Content ??= new StringContent("{\"value\":\"payload\"}", Encoding.UTF8, "application/json");
        }
    }
    private sealed class RuntimeMetadata : IBingDynamicApiRequestHandler
    { public ValueTask HandleAsync(string name, HttpRequestMessage request, CancellationToken token) { Metadata("runtime", request); return default; } }
    private sealed class MetadataHandler(string caller) : DelegatingHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Metadata(caller, request); return base.SendAsync(request, token); } }
    private sealed class Signer : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            request.Headers.Remove("X-Demo-Signature");
            request.Headers.Add("X-Demo-Signature", Guid.NewGuid().ToString("N")); // 真实签名使用当前分支的地址、正文和新凭据。
            return base.SendAsync(request, token);
        }
    }
    private sealed class Cache : IHttpSyntheticResponsePolicy
    {
        public ValueTask<HttpSyntheticResponse?> TryCreateAsync(HttpSyntheticRequest request, HttpSyntheticFailure failure, CancellationToken token) =>
            ValueTask.FromResult<HttpSyntheticResponse?>(new(new(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(new ExtensionDemoDto("cached")), Encoding.UTF8, "application/json") },
                HttpSyntheticResponseKind.Cached, Read.Name, "v1", request.AccessScope, "demo-cache", DateTimeOffset.UtcNow.AddMinutes(1)));
    }
    private sealed class Observer : IHttpResilienceResponseObserver
    {
        public ValueTask ObserveAsync(HttpResponseObservation response, CancellationToken token)
        { if (response.IsSynthetic) Console.WriteLine("合成响应来源：" + response.SourceId); return default; }
    }
}
