using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.Contracts;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Bing.Resilience;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

/// <summary>固定同服务地址池：主站慢响应，备用站返回，两种客户端共享相同配置。</summary>
internal static class HttpHedgingDemo
{
    internal static async Task RunAsync()
    {
        await using var primary = CreateServer(false);
        await using var secondary = CreateServer(true);
        await primary.StartAsync(); await secondary.StartAsync();
        var first = Address(primary); var second = Address(secondary);
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
        services.AddBingWebApiClient<IDemoSdk>(o => o.BaseAddress = new Uri(first))
            .AddBingHttpResilience("pool", Configure)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .AddHttpMessageHandler(() => new DemoSignatureHandler());
        services.AddBingDynamicApiClient(o => o.AddRemoteService("pool", remote => remote.BaseUrl = first));
        services.AddBingDynamicApiClient<IDemoAppService>("pool");
        services.AddBingDynamicApiClientResilience("pool", o => Configure(o.Http))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .AddHttpMessageHandler(() => new DemoSignatureHandler());
        using var provider = services.BuildServiceProvider();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sdk = await provider.GetRequiredService<IDemoSdk>().ReadAsync(budget.Token);
        var runtime = await provider.GetRequiredService<IDemoAppService>().ReadAsync(budget.Token);
        if (sdk.Client != "secondary" || runtime.Client != "secondary")
            throw new InvalidOperationException("备用端点未获胜。");
        Console.WriteLine("HTTP Hedging PASS: WebApiClient=secondary; DynamicApi.Client=secondary; definition=primary");
        await primary.StopAsync(); await secondary.StopAsync();

        void Configure(BingHttpResilienceOptions options)
        {
            options.Mode = BingHttpResilienceMode.Hedging;
            options.CanHedgeRequest = request => request.RequestUri!.AbsolutePath.EndsWith("/read", StringComparison.Ordinal);
            options.Hedging.Delay = TimeSpan.FromMilliseconds(100);
            options.Hedging.Endpoints.Add(new("primary", new Uri(first)));
            options.Hedging.Endpoints.Add(new("secondary", new Uri(second)));
            options.Hedging.SignatureHeaders.Add("X-Nonce");
            options.Hedging.SignatureHeaders.Add("X-Signature");
        }
    }
    private static WebApplication CreateServer(bool secondary)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/{client}/read", async (HttpContext context) =>
        {
            if (!secondary) await Task.Delay(TimeSpan.FromSeconds(1), context.RequestAborted);
            if (!context.Request.Headers.ContainsKey("X-Nonce")) return Results.StatusCode(401);
            return Results.Json(new DemoResult(secondary ? "secondary" : "primary", 1));
        });
        app.MapGet("/api/dynamic-api/v1/definition", () =>
        {
            if (secondary) throw new InvalidOperationException("描述不应路由至备用端点。");
            return Results.Json(new BingDynamicApiDefinition
            {
                ApiVersion = "v1",
                Services = { new BingDynamicApiServiceDefinition
                {
                    ServiceTypeName = typeof(IDemoAppService).FullName, ServiceName = "demo",
                    Methods = { new BingDynamicApiMethodDefinition
                    {
                        MethodName = nameof(IDemoAppService.ReadAsync), HttpMethod = "GET", RouteTemplate = "runtime/read",
                        Parameters = { new BingDynamicApiParameterDefinition
                        {
                            Position = 0, Name = "cancellationToken", TypeName = typeof(CancellationToken).FullName,
                            IsCancellationToken = true, BindingSource = "Special"
                        } }
                    } }
                } }
            });
        });
        return app;
    }
    private static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
