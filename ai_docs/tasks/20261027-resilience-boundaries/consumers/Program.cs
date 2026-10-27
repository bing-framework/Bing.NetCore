using System.Net;
using Bing.Resilience.Operations;
using Bing.Resilience.Replay;
using Bing.Resilience.Compensation;
using System.Text;
using System.Text.Json;
using Bing.Application.Services;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.Contracts;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApiClientCore.Attributes;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var services = new ServiceCollection();
var resilience = services.AddBingResilience();
#if CONSUMER_BUILTIN
resilience.AddBuiltInProvider("primary").AddBuiltInProvider("secondary");
const string engine = "BuiltIn";
#elif CONSUMER_FENCES
resilience.AddFencesProvider("primary").AddFencesProvider("secondary");
const string engine = "Fences";
#elif CONSUMER_POLLY
resilience.AddPollyProvider("primary").AddPollyProvider("secondary");
const string engine = "Polly";
#else
#error A consumer engine must be selected.
#endif
resilience.UseDefaultProvider("primary").AddRuntimeSwitching();
services.AddBingResiliencePipeline<string>("runtime-switch", options => options.SwitchGroup = "package-smoke");

var sdkCalls = 0;
var runtimeCalls = 0;
services.AddBingWebApiClient<IPackageSdk>(options => options.BaseAddress = new Uri("http://package.test"))
    .AddBingHttpResilience("package-sdk", options =>
    {
        options.ProviderName = "primary";
        options.CanRetryRequest = _ => true;
        options.Retry.Delay = TimeSpan.Zero;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new ProbeHandler(() =>
        new HttpResponseMessage(++sdkCalls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { Content = new StringContent("sdk-ok") }));

services.AddBingDynamicApiClient(options => options.AddRemoteService("probe", remote =>
{
    remote.BaseUrl = "http://package.test";
    remote.MaxRetries = 0;
}));
services.AddBingDynamicApiClient<IConsumerApp>("probe");
services.AddBingDynamicApiClientResilience("PROBE", options =>
{
    options.Http.CanRetryRequest = _ => true;
    options.Http.Retry.Delay = TimeSpan.Zero;
}).ConfigurePrimaryHttpMessageHandler(() => new ProbeHandler(
    () => new HttpResponseMessage(HttpStatusCode.OK),
    request => request.RequestUri!.AbsolutePath.EndsWith("/definition", StringComparison.Ordinal)
        ? new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new BingDynamicApiDefinition
            {
                ApiVersion = "v1",
                Services = { new BingDynamicApiServiceDefinition
                {
                    ServiceTypeName = typeof(IConsumerApp).FullName,
                    ServiceName = "consumer",
                    Methods = { new BingDynamicApiMethodDefinition
                    {
                        MethodName = "ReadAsync",
                        HttpMethod = "GET",
                        RouteTemplate = "runtime"
                    } }
                } }
            }), Encoding.UTF8, "application/json")
        }
        : new HttpResponseMessage(++runtimeCalls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { Content = new StringContent("runtime-ok") }));

using var provider = services.BuildServiceProvider();
var pipelineProvider = provider.GetRequiredService<IResiliencePipelineProvider>();
pipelineProvider.Validate();
var runtime = provider.GetRequiredService<IResilienceRuntimeManager>();
var before = runtime.GetSnapshot("package-smoke");
var switched = await runtime.SwitchAsync("package-smoke", "secondary", before.Version);
var runtimeResult = await pipelineProvider.GetPipeline<string>("runtime-switch")
    .ExecuteAsync(_ => new ValueTask<string>("runtime-switch-ok"));
if (runtimeResult != "runtime-switch-ok" || switched.Snapshot.ProviderName != "secondary")
    throw new InvalidOperationException("Runtime provider switch smoke failed.");

var now = DateTimeOffset.UtcNow;
var endpointSource = new ConfigurationServiceEndpointSource(
    new DelegateServiceEndpointTrustPolicy((_, _) => true), TimeProvider.System);
endpointSource.Publish(new ServiceEndpointSnapshot("catalog", "v1", now, now.AddMinutes(1), now.AddMinutes(2),
    new[] { new ServiceEndpoint("primary", "inc-1", new Uri("https://catalog.example/"), "contract-v1", "catalog", "catalog-api") }),
    expectedVersion: null);
var snapshot = await endpointSource.GetSnapshotAsync("catalog");
if (new OrderedServiceEndpointSelector().Select(snapshot).Count != 1)
    throw new InvalidOperationException("Discovery snapshot smoke failed.");
var weightedSnapshot = new ServiceEndpointSnapshot("weighted", "v1", now, now.AddMinutes(1), now.AddMinutes(2),
    new[] { snapshot.Endpoints[0] with { Weight = 3 }, snapshot.Endpoints[0] with { Id = "backup", Weight = 1 } });
var weighted = new WeightedRoundRobinServiceEndpointSelector();
if (Enumerable.Range(0, 100).Count(_ => weighted.Select(weightedSnapshot)[0].Id == "primary") != 75)
    throw new InvalidOperationException("Packaged weighted selector did not distribute 3:1 traffic.");
ResilienceDiagnostics.RecordLifecycle("consumer.smoke", "success");

var sdkResult = await provider.GetRequiredService<IPackageSdk>().ReadAsync();
var remoteOptions = provider.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value.RemoteServices["probe"];
var appResult = await provider.GetRequiredService<IConsumerApp>().ReadAsync();
if (sdkResult != "sdk-ok" || sdkCalls != 2 || remoteOptions.RetryMode != BingDynamicApiRetryMode.HttpPipeline ||
    remoteOptions.OperationTimeout != TimeSpan.FromSeconds(10) || appResult != "runtime-ok" || runtimeCalls != 2)
    throw new InvalidOperationException("Packaged HTTP client smoke failed.");


// 新可选包的独立消费：嵌入迁移、只读校验、桥接 API 与管理注册均来自 nupkg。
var dbPath = Path.Combine(Path.GetTempPath(), "bing-boundary-consumer-" + Guid.NewGuid().ToString("N") + ".db");
try
{
    var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
    var store = new Bing.Resilience.Operations.Relational.RelationalOperationStore(
        _ => ValueTask.FromResult<System.Data.Common.DbConnection>(new Microsoft.Data.Sqlite.SqliteConnection(connectionString)),
        Bing.Resilience.Operations.Relational.ResilienceRelationalDialect.Sqlite);
    await using (var database = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
    {
        await database.OpenAsync(); await using var command = database.CreateCommand();
        command.CommandText = store.GetMigrationScript(); await command.ExecuteNonQueryAsync();
    }
    await store.ValidateSchemaAsync();
    await using (var transaction = await store.BeginAsync())
    {
        var old = now.AddDays(-30);
        await transaction.TryInsertAsync(new OperationRecord("retention-smoke", "replay", OperationFingerprint.Hash("scope"), OperationFingerprint.Hash("key"), OperationFingerprint.Hash("fingerprint"),
            "Succeeded", 0, "sensitive-payload", 0, old, old, old, old));
        await transaction.CommitAsync();
    }
    var retention = new OperationRetentionPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(7));
    var cleaned = await ((IOperationRetentionStore)store).CleanupAsync("replay", retention, _ => "metadata");
    await using (var transaction = await store.BeginAsync())
    {
        var record = await transaction.FindByKeyAsync("replay", OperationFingerprint.Hash("scope"), OperationFingerprint.Hash("key"));
        if (cleaned.Payloads != 1 || record is null || !OperationRetainedData.IsRedacted(record.Data) ||
            OperationRetainedData.Unwrap(record.Data) != "metadata" || record.Fingerprint != OperationFingerprint.Hash("fingerprint"))
            throw new InvalidOperationException("Packaged retention lost dedupe identity or retained payload.");
    }
    using var context = new Microsoft.EntityFrameworkCore.DbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder().Options);
    try { Bing.Resilience.Operations.EntityFrameworkCore.EntityFrameworkOperationExtensions.EnlistEntityFramework(store, context); throw new Exception("missing transaction accepted"); }
    catch (InvalidOperationException) { }
    if (typeof(Bing.Resilience.Operations.FreeSql.FreeSqlOperationExtensions).GetMethod("EnlistFreeSql") is null) throw new Exception("bridge absent");
    var management = new ServiceCollection();
    Bing.Resilience.Operations.AspNetCore.ResilienceOperationsEndpointExtensions.AddBingResilienceOperationsManagement(management);
    var webBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
    webBuilder.Services.AddBingResilienceOperations(new ResilienceOperationDescriptor("consumer.write", "v1", ResilienceOperationKind.IdempotentWrite, "tenant"));
    webBuilder.Services.AddSingleton<IResilienceOperationStore>(store);
    webBuilder.Services.AddSingleton<IOperationDataProtector>(_ => new AesGcmOperationDataProtector("test", new Dictionary<string,byte[]> { ["test"] = new byte[32] }));
    webBuilder.Services.AddBingDurableReplay(o => o.Authorize = (_, _) => ValueTask.FromResult(true), new ReplayHandlerRegistration("test", "1", "consumer.write", _ => new ConsumerHandler()));
    webBuilder.Services.AddBingSaga(o => o.Authorize = (_, _) => ValueTask.FromResult(true), new SagaHandlerRegistration("test", "1", _ => new ConsumerHandler()));
    foreach (var descriptor in management) webBuilder.Services.Add(descriptor);
    await using var web = webBuilder.Build();
    Bing.Resilience.Operations.AspNetCore.ResilienceOperationsEndpointExtensions.MapBingResilienceOperations(web, o =>
    { o.ResolveAccessScope = _ => ValueTask.FromResult<string?>("tenant"); o.AuthorizeAsync = (_, _) => ValueTask.FromResult(false); o.EnableConsole = true; });
    using var registry = new ConsulServiceEndpointSource(new ConsulEndpointOptions
    { ServiceName = "catalog", BaseAddress = new Uri("https://registry.test/"), TrustBaseAddress = _ => true, ValidateCustomTransport = _ => true, CredentialAudience = "catalog" },
        new ProbeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }),
        new DelegateServiceEndpointTrustPolicy((_, _) => true), TimeProvider.System);
    if ((await registry.GetSnapshotAsync("catalog")).Endpoints.Count != 0) throw new Exception("registry smoke");
}
finally { if (File.Exists(dbPath)) File.Delete(dbPath); }

Console.WriteLine($"PACKAGE_CONSUMER_PASS engine={engine} runtime={Environment.Version} switch={switched.Snapshot.Version} discovery={snapshot.SourceVersion} sdkAttempts={sdkCalls} runtimeAttempts={runtimeCalls}");

public interface IPackageSdk
{
    [HttpGet("read")]
    [RawReturn]
    Task<string> ReadAsync();
}

public interface IConsumerApp : IAppService
{
    Task<string> ReadAsync();
}

internal sealed class ProbeHandler(
    Func<HttpResponseMessage> response,
    Func<HttpRequestMessage, HttpResponseMessage>? requestResponse = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(requestResponse?.Invoke(request) ?? response());
}

internal sealed class ConsumerHandler : IReplayCommandHandler, ISagaStepHandler
{
    public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext c, CancellationToken t) => ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
    public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext c, CancellationToken t) => ExecuteAsync(c,t);
    public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext c, CancellationToken t) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
    public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext c, CancellationToken t) => ForwardAsync(c,t);
    public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext c, CancellationToken t) => ForwardAsync(c,t);
}
