using System.Net;
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

var sdkResult = await provider.GetRequiredService<IPackageSdk>().ReadAsync();
var remoteOptions = provider.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value.RemoteServices["probe"];
var appResult = await provider.GetRequiredService<IConsumerApp>().ReadAsync();
if (sdkResult != "sdk-ok" || sdkCalls != 2 || remoteOptions.RetryMode != BingDynamicApiRetryMode.HttpPipeline ||
    remoteOptions.OperationTimeout != TimeSpan.FromSeconds(10) || appResult != "runtime-ok" || runtimeCalls != 2)
    throw new InvalidOperationException("Packaged HTTP client smoke failed.");

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
