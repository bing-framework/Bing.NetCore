using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Bing.Application.Services;
using Bing.Content;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Client.Resilience;
using Bing.DynamicApi.Contracts;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Clients;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public interface IHedgingSafetyProbe : IAppService
{
    Task<string> GetWithBodyAsync(string body, CancellationToken cancellationToken = default);
    Task<string> WriteAsync(CancellationToken cancellationToken = default);
    Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken = default);
}

public sealed class HedgingLoopbackTests
{
    [Fact]
    public async Task RealWebApiClientHedgesSlowPrimaryToFastBackup()
    {
        var primaryCalls = 0;
        var backupCalls = 0;
        await using var primary = new HedgingLoopbackServer((request, _) =>
        {
            if (request.Path == "/read")
            {
                Interlocked.Increment(ref primaryCalls);
                return Task.FromResult(HedgingLoopbackResponse.Hang());
            }
            return Task.FromResult(HedgingLoopbackResponse.Json("not found", HttpStatusCode.NotFound));
        });
        await using var backup = new HedgingLoopbackServer((request, _) =>
        {
            if (request.Path == "/read")
            {
                Interlocked.Increment(ref backupCalls);
                return Task.FromResult(HedgingLoopbackResponse.Json(JsonSerializer.Serialize(new SdkResult("backup"))));
            }
            return Task.FromResult(HedgingLoopbackResponse.Json("not found", HttpStatusCode.NotFound));
        });

        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingWebApiClient<IResilientSdk>(options => options.BaseAddress = primary.Origin)
            .AddBingHttpResilience("webapi-hedge", options => ConfigureHttpHedging(
                options, primary.Origin, backup.Origin, request => request.RequestUri!.AbsolutePath == "/read"))
            .ConfigurePrimaryHttpMessageHandler(SafeSocketsHandler);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IResilientSdk>().ReadAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("backup", result.Value);
        Assert.Equal(1, primaryCalls);
        Assert.Equal(1, backupCalls);
    }

    [Fact]
    public async Task RealDynamicClientRetriesDefinitionOnPrimaryThenHedgesBusinessRead()
    {
        var primaryDefinitions = 0;
        var backupDefinitions = 0;
        var primaryCalls = 0;
        var backupCalls = 0;
        await using var primary = new HedgingLoopbackServer((request, _) =>
        {
            if (request.Path.EndsWith("/definition", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref primaryDefinitions) == 1)
                    return Task.FromResult(HedgingLoopbackResponse.Json("busy", HttpStatusCode.ServiceUnavailable));
                return Task.FromResult(HedgingLoopbackResponse.Json(JsonSerializer.Serialize(RuntimeProbeDefinition())));
            }

            if (request.Path.EndsWith(nameof(IProbe.GetAsync), StringComparison.Ordinal))
            {
                Interlocked.Increment(ref primaryCalls);
                return Task.FromResult(HedgingLoopbackResponse.Hang());
            }

            return Task.FromResult(HedgingLoopbackResponse.Json("not found", HttpStatusCode.NotFound));
        });
        await using var backup = new HedgingLoopbackServer((request, _) =>
        {
            if (request.Path.EndsWith("/definition", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref backupDefinitions);
                return Task.FromResult(HedgingLoopbackResponse.Json("definition must stay on primary", HttpStatusCode.BadRequest));
            }
            if (request.Path.EndsWith(nameof(IProbe.GetAsync), StringComparison.Ordinal))
            {
                Interlocked.Increment(ref backupCalls);
                return Task.FromResult(HedgingLoopbackResponse.Json(JsonSerializer.Serialize("hedged")));
            }
            return Task.FromResult(HedgingLoopbackResponse.Json("not found", HttpStatusCode.NotFound));
        });

        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingDynamicApiClient(options => options.AddRemoteService("probe", remote => remote.BaseUrl = primary.Origin.ToString()));
        services.AddBingDynamicApiClient<IProbe>("probe");
        services.AddBingDynamicApiClientResilience("probe", options => ConfigureRuntimeHedging(
            options, primary.Origin, backup.Origin, maxRetries: 1))
            .ConfigurePrimaryHttpMessageHandler(SafeSocketsHandler);
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IProbe>().GetAsync().WaitAsync(TimeSpan.FromSeconds(8));

        Assert.Equal("hedged", result);
        Assert.Equal(2, primaryDefinitions);
        Assert.Equal(0, backupDefinitions);
        Assert.Equal(1, primaryCalls);
        Assert.Equal(1, backupCalls);
    }

    [Fact]
    public async Task RuntimeDoesNotHedgePostBodyGetOrStreamCalls()
    {
        var definitionRequests = 0;
        var primaryInvocations = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var backupInvocations = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        await using var primary = new HedgingLoopbackServer((request, _) =>
        {
            if (request.Path.EndsWith("/definition", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref definitionRequests);
                return Task.FromResult(HedgingLoopbackResponse.Json(JsonSerializer.Serialize(SafetyDefinition())));
            }

            primaryInvocations.AddOrUpdate(request.Path, 1, (_, count) => count + 1);
            if (request.Path.EndsWith(nameof(IHedgingSafetyProbe.GetWithBodyAsync), StringComparison.Ordinal))
                Assert.Equal("\"request body\"", request.Body);
            return Task.FromResult(HedgingLoopbackResponse.Json("busy", HttpStatusCode.ServiceUnavailable));
        });
        await using var backup = new HedgingLoopbackServer((request, _) =>
        {
            backupInvocations.AddOrUpdate(request.Path, 1, (_, count) => count + 1);
            return Task.FromResult(HedgingLoopbackResponse.Json(JsonSerializer.Serialize("unexpected backup")));
        });

        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingDynamicApiClient(options => options.AddRemoteService("probe", remote => remote.BaseUrl = primary.Origin.ToString()));
        services.AddBingDynamicApiClient<IHedgingSafetyProbe>("probe");
        services.AddBingDynamicApiClientResilience("probe", options => ConfigureRuntimeHedging(
            options, primary.Origin, backup.Origin, maxRetries: 0))
            .ConfigurePrimaryHttpMessageHandler(SafeSocketsHandler);
        using var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IHedgingSafetyProbe>();

        await Assert.ThrowsAsync<BingRemoteCallException>(() => api.GetWithBodyAsync("request body"));
        await Assert.ThrowsAsync<BingRemoteCallException>(() => api.WriteAsync());
        await Assert.ThrowsAsync<BingRemoteCallException>(() => api.DownloadAsync());

        Assert.Equal(1, definitionRequests);
        Assert.Equal(1, primaryInvocations["/api/probe/GetWithBodyAsync"]);
        Assert.Equal(1, primaryInvocations["/api/probe/WriteAsync"]);
        Assert.Equal(1, primaryInvocations["/api/probe/DownloadAsync"]);
        Assert.Empty(backupInvocations);
    }

    private static void ConfigureHttpHedging(BingHttpResilienceOptions options, Uri primary, Uri backup,
        Func<HttpRequestMessage, bool> canHedge)
    {
        options.Mode = BingHttpResilienceMode.Hedging;
        options.CanHedgeRequest = canHedge;
        options.Retry.MaxRetryAttempts = 0;
        options.CircuitBreaker.Enabled = false;
        options.Hedging.MaxHedgedAttempts = 1;
        options.Hedging.Delay = TimeSpan.FromMilliseconds(30);
        options.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("primary", primary));
        options.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("backup", backup));
    }

    private static void ConfigureRuntimeHedging(BingDynamicApiClientResilienceOptions options,
        Uri primary, Uri backup, int maxRetries)
    {
        options.Http.Mode = BingHttpResilienceMode.Hedging;
        options.Http.CanHedgeRequest = _ => true;
        options.Http.Retry.MaxRetryAttempts = maxRetries;
        options.Http.Retry.Delay = TimeSpan.Zero;
        options.Http.Retry.MaxDelay = TimeSpan.Zero;
        options.Http.Retry.UseJitter = false;
        options.Http.CircuitBreaker.Enabled = false;
        options.Http.Hedging.MaxHedgedAttempts = 1;
        options.Http.Hedging.Delay = TimeSpan.FromMilliseconds(30);
        options.Http.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("primary", primary));
        options.Http.Hedging.Endpoints.Add(new BingHttpHedgingEndpoint("backup", backup));
        options.OperationTimeout = TimeSpan.FromSeconds(8);
        options.DefinitionTimeout = TimeSpan.FromSeconds(5);
    }

    private static SocketsHttpHandler SafeSocketsHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };

    private static BingDynamicApiDefinition SafetyDefinition() => new()
    {
        ApiVersion = "v1",
        Services =
        {
            new BingDynamicApiServiceDefinition
            {
                ServiceTypeName = typeof(IHedgingSafetyProbe).FullName!,
                ServiceName = "hedging-probe",
                Methods =
                {
                    SafetyMethod(nameof(IHedgingSafetyProbe.GetWithBodyAsync), "GET", false,
                        new BingDynamicApiParameterDefinition
                        {
                            Name = "body", Position = 0, TypeName = typeof(string).FullName!, BindingSource = "Body"
                        },
                        CancellationParameter("cancellationToken", 1)),
                    SafetyMethod(nameof(IHedgingSafetyProbe.WriteAsync), "POST", false,
                        CancellationParameter("cancellationToken", 0)),
                    SafetyMethod(nameof(IHedgingSafetyProbe.DownloadAsync), "GET", true,
                        CancellationParameter("cancellationToken", 0))
                }
            }
        }
    };

    private static BingDynamicApiDefinition RuntimeProbeDefinition() => new()
    {
        ApiVersion = "v1",
        Services =
        {
            new BingDynamicApiServiceDefinition
            {
                ServiceTypeName = typeof(IProbe).FullName!,
                ServiceName = "probe",
                Methods =
                {
                    RuntimeProbeMethod(nameof(IProbe.GetAsync), "GET", false),
                    RuntimeProbeMethod(nameof(IProbe.DownloadAsync), "GET", true),
                    RuntimeProbeMethod(nameof(IProbe.WriteAsync), "POST", false)
                }
            }
        }
    };

    private static BingDynamicApiMethodDefinition RuntimeProbeMethod(string name, string verb, bool stream) => new()
    {
        MethodName = name,
        HttpMethod = verb,
        RouteTemplate = "api/probe/" + name,
        ReturnTypeName = stream ? typeof(IRemoteStreamContent).FullName! : typeof(string).FullName!,
        IsStreamResult = stream,
        Parameters = { CancellationParameter("cancellationToken", 0) }
    };

    private static BingDynamicApiMethodDefinition SafetyMethod(string name, string verb, bool stream,
        params BingDynamicApiParameterDefinition[] parameters) => new()
    {
        MethodName = name,
        HttpMethod = verb,
        RouteTemplate = "api/probe/" + name,
        ReturnTypeName = stream ? typeof(IRemoteStreamContent).FullName! : typeof(string).FullName!,
        IsStreamResult = stream,
        Parameters = parameters.ToList()
    };

    private static BingDynamicApiParameterDefinition CancellationParameter(string name, int position) => new()
    {
        Name = name,
        Position = position,
        TypeName = typeof(CancellationToken).FullName!,
        BindingSource = "Special",
        IsCancellationToken = true
    };
}
