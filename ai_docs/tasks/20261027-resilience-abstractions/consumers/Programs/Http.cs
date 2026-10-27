using Bing.Resilience;
using Bing.Application.Services;
using Bing.DynamicApi.Contracts;
using System.Text;
using System.Text.Json;
using System.Net;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApiClientCore.Attributes;

var services = new ServiceCollection();
#if POLLY
services.AddBingResilience().AddPollyProvider("primary").UseDefaultProvider("primary");
#else
services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
#endif
int calls = 0, runtimeCalls = 0;
services.AddBingWebApiClient<IPackageSdk>(o => o.BaseAddress = new Uri("http://package.test"))
    .AddBingHttpResilience("consumer", o =>
    {
        o.CanRetryRequest = _ => true;
        o.Retry.Delay = TimeSpan.Zero;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new Handler(() =>
        new HttpResponseMessage(++calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { Content = new StringContent("package-ok") }));
services.AddBingDynamicApiClient(o => o.AddRemoteService("probe", r => r.BaseUrl = "http://package.test"));
services.AddBingDynamicApiClient<IConsumerApp>("probe");
services.AddBingDynamicApiClientResilience("PROBE", o => { o.Http.CanRetryRequest = _ => true; o.Http.Retry.Delay = TimeSpan.Zero; })
    .ConfigurePrimaryHttpMessageHandler(() => new Handler(() => new HttpResponseMessage(HttpStatusCode.OK), request =>
    request.RequestUri!.AbsolutePath.EndsWith("/definition")
        ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new BingDynamicApiDefinition
        {
            ApiVersion = "v1", Services = { new BingDynamicApiServiceDefinition
            {
                ServiceTypeName = typeof(IConsumerApp).FullName, ServiceName = "consumer",
                Methods = { new BingDynamicApiMethodDefinition { MethodName = "ReadAsync", HttpMethod = "GET", RouteTemplate = "runtime" } }
            } }
        }), Encoding.UTF8, "application/json") }
        : new HttpResponseMessage(++runtimeCalls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent("runtime-package-ok") }));
using var provider = services.BuildServiceProvider();
if (await provider.GetRequiredService<IPackageSdk>().ReadAsync() != "package-ok" || calls != 2)
    throw new InvalidOperationException("Package HTTP pipeline failed.");
var options = provider.GetRequiredService<IOptions<BingDynamicApiClientOptions>>().Value.RemoteServices["probe"];
if (options.RetryMode != BingDynamicApiRetryMode.HttpPipeline || options.OperationTimeout != TimeSpan.FromSeconds(10))
    throw new InvalidOperationException("Package runtime adapter failed.");
if (await provider.GetRequiredService<IConsumerApp>().ReadAsync() != "runtime-package-ok" || runtimeCalls != 2)
    throw new InvalidOperationException("Packaged runtime proxy failed.");
Console.WriteLine($"Package consumer PASS: runtime={Environment.Version}, SDK attempts={calls}, adapter={options.RetryMode}, runtime attempts={runtimeCalls}");

public interface IPackageSdk
{
    [HttpGet("read")][RawReturn] Task<string> ReadAsync();
}
public interface IConsumerApp : IAppService { Task<string> ReadAsync(); }
internal sealed class Handler(Func<HttpResponseMessage> respond, Func<HttpRequestMessage, HttpResponseMessage>? requestRespond = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(requestRespond?.Invoke(request) ?? respond());
}
