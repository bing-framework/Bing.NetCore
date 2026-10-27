using Bing.Resilience;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Bing.Application.Services;
using Bing.Content;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.Contracts;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using WebApiClientCore.Attributes;
using WebApiClientCore.Parameters;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (args.Contains("--hedging"))
{
    await HttpHedgingDemo.RunAsync();
    return;
}
if (args.Contains("--probe"))
{
    await ResourceProbe.RunAsync();
    return;
}

// 本地可控下游：每条读取路径首次 503，后续成功；不会访问外部服务。
var host = WebApplication.CreateBuilder();
host.Logging.ClearProviders();
host.WebHost.UseUrls("http://127.0.0.1:0");
await using var app = host.Build();
var attempts = new ConcurrentDictionary<string, int>();
var nonces = new ConcurrentDictionary<string, List<string>>();
app.MapGet("/{client}/read", (HttpContext context, string client) =>
{
    var count = attempts.AddOrUpdate(client, 1, (_, n) => n + 1);
    var values = nonces.GetOrAdd(client, _ => new());
    lock (values) values.Add(context.Request.Headers["X-Nonce"].ToString());
    return count == 1 ? Results.StatusCode(503) : Results.Json(new DemoResult(client, count));
});
app.MapGet("/{client}/download", (string client) => Results.File(Encoding.UTF8.GetBytes(client + "-file"), "text/plain", "demo.txt"));
app.MapPost("/sdk/upload", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
    return Results.Text($"uploaded:{form.Files.Single().Length}");
});
app.MapGet("/slow", async (HttpContext context) =>
{
    context.Response.ContentType = "application/json";
    await context.Response.StartAsync(context.RequestAborted);
    await context.Response.Body.FlushAsync(context.RequestAborted);
    await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
});
app.MapGet("/api/dynamic-api/v1/definition", () => Results.Json(new BingDynamicApiDefinition
{
    ApiVersion = "v1",
    Services =
    {
        new BingDynamicApiServiceDefinition
        {
            ServiceTypeName = typeof(IDemoAppService).FullName, ServiceName = "demo",
            Methods = { Describe(nameof(IDemoAppService.ReadAsync), "runtime/read", false),
                        Describe(nameof(IDemoAppService.DownloadAsync), "runtime/download", true) }
        }
    }
}));
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

var clientHostBuilder = Host.CreateApplicationBuilder();
clientHostBuilder.Logging.ClearProviders();
var services = clientHostBuilder.Services;
services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
services.AddBingWebApiClient<IDemoSdk>(o => o.BaseAddress = new Uri(address))
    .AddBingHttpResilience("demo-read", o => o.CanRetryRequest = r => r.RequestUri!.AbsolutePath == "/sdk/read")
    .AddHttpMessageHandler(() => new DemoSignatureHandler());
services.AddBingDynamicApiClient(o => o.AddRemoteService("demo", r =>
{
    r.BaseUrl = address;
    r.MaxRetries = 0;
}));
services.AddBingDynamicApiClient<IDemoAppService>("DEMO");
services.AddBingDynamicApiClientResilience("demo", o =>
    o.Http.CanRetryRequest = r => r.RequestUri!.AbsolutePath == "/runtime/read")
    .AddHttpMessageHandler(() => new DemoSignatureHandler());
using var clientHost = clientHostBuilder.Build();
await clientHost.StartAsync();
var provider = clientHost.Services;
var sdk = provider.GetRequiredService<IDemoSdk>();
var runtime = provider.GetRequiredService<IDemoAppService>();
using var caller = new CancellationTokenSource();
using var budget = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
budget.CancelAfter(TimeSpan.FromSeconds(10));
var sdkResult = await sdk.ReadAsync(budget.Token);
var runtimeResult = await runtime.ReadAsync(caller.Token);
if (sdkResult.Attempts != 2 || runtimeResult.Attempts != 2 || nonces.Any(p => p.Value.Distinct().Count() != 2))
    throw new InvalidOperationException("故障恢复或逐次签名验证失败。");
Console.WriteLine($"SDK={sdkResult.Attempts} 次；Runtime={runtimeResult.Attempts} 次；每次签名已刷新。");

// 文件路径不在重试白名单；返回后由调用方提供取消令牌并释放。
using (var response = await sdk.DownloadAsync(caller.Token))
    Console.WriteLine(await response.Content.ReadAsStringAsync(caller.Token));
using (var content = await runtime.DownloadAsync(caller.Token))
using (var reader = new StreamReader(content.GetStream(), Encoding.UTF8))
    Console.WriteLine(await reader.ReadToEndAsync(caller.Token));
Console.WriteLine(await sdk.UploadAsync(new FormDataFile(Encoding.UTF8.GetBytes("payload"), "demo.txt"), caller.Token));
using (var slowBudget = CancellationTokenSource.CreateLinkedTokenSource(caller.Token))
{
    slowBudget.CancelAfter(TimeSpan.FromMilliseconds(300));
    try
    {
        await sdk.SlowAsync(slowBudget.Token);
        throw new InvalidOperationException("响应体预算未生效。");
    }
    catch (HttpRequestException e) when (e.InnerException is OperationCanceledException)
    {
        Console.WriteLine("成功响应体挂起：调用方预算已取消，SDK 保留 HttpRequestException 包装。");
    }
}
await clientHost.StopAsync();
await app.StopAsync();
Console.WriteLine("HTTP 故障样例验证通过。");

static BingDynamicApiMethodDefinition Describe(string name, string route, bool stream) => new()
{
    MethodName = name, HttpMethod = "GET", RouteTemplate = route, IsStreamResult = stream,
    ReturnTypeName = stream ? typeof(IRemoteStreamContent).FullName : typeof(DemoResult).FullName,
    Parameters =
    {
        new BingDynamicApiParameterDefinition
        {
            Position = 0, Name = "cancellationToken", TypeName = typeof(CancellationToken).FullName,
            IsCancellationToken = true, BindingSource = "Special"
        }
    }
};

public sealed record DemoResult(string Client, int Attempts);

public interface IDemoSdk
{
    [HttpGet("sdk/read")][JsonReturn] Task<DemoResult> ReadAsync(CancellationToken cancellationToken);
    [HttpGet("sdk/download")][RawReturn] Task<HttpResponseMessage> DownloadAsync(CancellationToken cancellationToken);
    [HttpPost("sdk/upload")][RawReturn] Task<string> UploadAsync(FormDataFile file, CancellationToken cancellationToken);
    [HttpGet("slow")][JsonReturn] Task<DemoResult> SlowAsync(CancellationToken cancellationToken);
}
public interface IDemoAppService : IAppService
{
    Task<DemoResult> ReadAsync(CancellationToken cancellationToken);
    Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken);
}

/// <summary>演示每次尝试刷新签名；实际系统应从安全凭据源取得密钥并遵守下游签名协议。</summary>
internal sealed class DemoSignatureHandler : DelegatingHandler
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(request.RequestUri + "|" + nonce));
        request.Headers.Remove("X-Nonce");
        request.Headers.Remove("X-Signature");
        request.Headers.TryAddWithoutValidation("X-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-Signature", Convert.ToBase64String(signature));
        return base.SendAsync(request, token);
    }
}
