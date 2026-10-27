using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations.AspNetCore;
using Bing.Resilience.Replay;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class OperationsManagementTests
{
    [Fact]
    public async Task ManagementRequiresAuthenticatedAuthorizedTenantAndRedactsPayload()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/_resilience/replay/order")).StatusCode);
        fixture.Client.DefaultRequestHeaders.Add("X-Test-User", "denied");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/_resilience/replay/order")).StatusCode);
        fixture.User("operator");
        var response = await fixture.Client.GetAsync("/_resilience/replay/order");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-payload", text); Assert.DoesNotContain("keyHash", text); Assert.DoesNotContain("leaseToken", text);
        fixture.Client.DefaultRequestHeaders.Add("X-Test-Scope", "other");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/_resilience/replay/order")).StatusCode);
    }

    [Fact]
    public async Task CancelRequiresAntiforgeryAndCasAndPersistsAuthenticatedAudit()
    {
        await using var fixture = await Fixture.Start(); fixture.User("operator");
        var state = (await fixture.Client.GetFromJsonAsync<ResilienceOperationStatus>("/_resilience/replay/order"))!;
        var payload = new CancelReplayRequest(state.Version, "operator cancelled duplicate order");
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/_resilience/replay/order/cancel", payload)).StatusCode);
        await fixture.Session();
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync("/_resilience/replay/order/cancel", payload with { ExpectedVersion = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/_resilience/replay/order/cancel", payload)).StatusCode);
        Assert.Equal("Cancelled", (await fixture.Client.GetFromJsonAsync<ResilienceOperationStatus>("/_resilience/replay/order"))!.State);
        await using var db = new SqliteConnection(fixture.Database.ConnectionString);
        var audit = await db.QuerySingleAsync<(string Actor, string Reason)>($"SELECT actor,reason FROM {fixture.Database.TablePrefix}_audit WHERE event_type='replay.cancelled'");
        Assert.Equal("operator", audit.Actor); Assert.Equal(payload.Reason, audit.Reason);
    }

    [Fact]
    public async Task SagaManualActionUsesCoordinatorCasAndUnknownConfirmation()
    {
        await using var fixture = await Fixture.Start(); fixture.User("operator"); await fixture.Session();
        var coordinator = fixture.App.Services.GetRequiredService<ISagaCoordinator>();
        await coordinator.StartAsync(new("saga", "orders.manage", "v1", "tenant", "saga-key",
            new[] { new SagaStepDefinition("reserve", "step", "1", "secret-input") }, DateTimeOffset.UtcNow.AddMinutes(5)));
        await fixture.App.Services.GetRequiredService<SagaWorker>().RunOnceAsync();
        var snapshot = (await fixture.Client.GetFromJsonAsync<ResilienceSagaStatus>("/_resilience/saga/saga"))!;
        Assert.Equal("Unknown", Assert.Single(snapshot.Steps).State);
        var url = "/_resilience/saga/saga/steps/reserve/manual";
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(url, new SagaManualRequest(99, SagaManualAction.ConfirmSucceeded, "confirmed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync(url,
            new SagaManualRequest(snapshot.Saga.Version, SagaManualAction.ConfirmSucceeded, "verified remote receipt"))).StatusCode);
        Assert.Equal("Succeeded", Assert.Single((await coordinator.GetAsync("saga", "tenant"))!.Steps).State);
    }

    [Fact]
    public async Task ManualCompensationRequiresCsrfUsesCasAndAuditsOnlyOnce()
    {
        var handler = new CountingCompensationHandler();
        await using var fixture = await Fixture.Start(handler); fixture.User("operator");
        var coordinator = fixture.App.Services.GetRequiredService<ISagaCoordinator>();
        const string sagaId = "saga-manual-compensation";
        await coordinator.StartAsync(new(sagaId, "orders.manage", "v1", "tenant", "saga-manual-key",
            new[] { new SagaStepDefinition("reserve", "step", "1", "secret-input", CanCompensate: true) },
            DateTimeOffset.UtcNow.AddMinutes(5)));
        var worker = fixture.App.Services.GetRequiredService<SagaWorker>();
        Assert.True(await worker.RunOnceAsync());

        var url = $"/_resilience/saga/{sagaId}/steps/reserve/manual";
        var snapshot = (await fixture.Client.GetFromJsonAsync<ResilienceSagaStatus>($"/_resilience/saga/{sagaId}"))!;
        Assert.Equal("Succeeded", Assert.Single(snapshot.Steps).State);
        var request = new SagaManualRequest(snapshot.Saga.Version, SagaManualAction.Compensate, "operator verified duplicate charge");
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync(url, request)).StatusCode);
        Assert.Equal(0, handler.CompensationCount);

        await fixture.Session();
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(url,
            request with { ExpectedSagaVersion = snapshot.Saga.Version + 1 })).StatusCode);
        Assert.Equal(0, handler.CompensationCount);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync(url, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(url, request)).StatusCode);

        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("Compensated", (await fixture.Client.GetFromJsonAsync<ResilienceSagaStatus>($"/_resilience/saga/{sagaId}"))!.Saga.State);
        Assert.Equal(1, handler.CompensationCount);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(url, request)).StatusCode);
        Assert.False(await worker.RunOnceAsync());
        Assert.Equal(1, handler.CompensationCount);

        await using var db = new SqliteConnection(fixture.Database.ConnectionString);
        var auditTable = fixture.Database.TablePrefix + "_audit";
        var auditCount = await db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {auditTable} WHERE operation_id=@SagaId AND event_type='saga.manual.compensate'", new { SagaId = sagaId });
        var audit = await db.QuerySingleAsync<(string Actor, string Reason)>(
            $"SELECT actor,reason FROM {auditTable} WHERE operation_id=@SagaId AND event_type='saga.manual.compensate'", new { SagaId = sagaId });
        Assert.Equal(1, auditCount);
        Assert.Equal("operator", audit.Actor);
        Assert.Equal(request.Reason, audit.Reason);
    }

    [Fact]
    public async Task ConsoleIsOptInAuthenticatedAndProtectedByCsp()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/_resilience")).StatusCode);
        fixture.User("operator");
        var response = await fixture.Client.GetAsync("/_resilience"); var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("text/html", response.Content.Headers.ContentType!.ToString());
        Assert.Contains("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.DoesNotContain("{{", html); Assert.Contains("textContent", html); Assert.DoesNotContain("innerHTML", html);
        Assert.Contains("data-session=\"/_resilience/session\"", html);
    }

    [Fact]
    public async Task AntiforgeryTokenIsBoundToOperatorAndRegistrationOptionsAreFrozen()
    {
        await using var fixture = await Fixture.Start(); fixture.User("operator"); await fixture.Session(); fixture.User("operator-two");
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/_resilience/replay/order/cancel", new CancelReplayRequest(0, "test"))).StatusCode);
        // Fixture 在映射后将原 AuthorizeAsync 改成 false；已发布的端点仍使用注册时委托。
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/_resilience/replay/order")).StatusCode);
    }

    [Fact]
    public async Task RegistrationRejectsMissingAuthorizationAndInvalidRoutes()
    {
        var builder = WebApplication.CreateBuilder(); builder.Services.AddBingResilienceOperationsManagement();
        await using var app = builder.Build();
        Assert.Throws<ResilienceConfigurationException>(() => app.MapBingResilienceOperations(_ => { }));
        Assert.Throws<ResilienceConfigurationException>(() => app.MapBingResilienceOperations(o =>
        { o.ReplayPrefix = "/bad/{id}"; o.ResolveAccessScope = _ => ValueTask.FromResult<string?>("tenant"); o.AuthorizeAsync = (_, _) => ValueTask.FromResult(true); }));
        Assert.Throws<ResilienceConfigurationException>(() => app.MapBingResilienceOperations(o =>
        { o.ConsolePath = "/_resilience/replay/admin"; o.ResolveAccessScope = _ => ValueTask.FromResult<string?>("tenant"); o.AuthorizeAsync = (_, _) => ValueTask.FromResult(true); }));
        var missing = Assert.Throws<ResilienceConfigurationException>(() => app.MapBingResilienceOperations(o =>
        { o.ResolveAccessScope = _ => ValueTask.FromResult<string?>("tenant"); o.AuthorizeAsync = (_, _) => ValueTask.FromResult(true); }));
        Assert.Contains("协调器", missing.Message);
    }

    private sealed class Fixture(WebApplication app, DurableSqliteDatabase database, HttpClient client) : IAsyncDisposable
    {
        internal WebApplication App => app;
        internal DurableSqliteDatabase Database => database;
        internal HttpClient Client => client;
        internal void User(string user) { client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", user); }
        internal async Task Session()
        {
            var json = await client.GetFromJsonAsync<JsonElement>("/_resilience/session");
            var header = json.GetProperty("headerName").GetString()!;
            client.DefaultRequestHeaders.Remove(header); client.DefaultRequestHeaders.Add(header, json.GetProperty("token").GetString());
        }
        internal static async Task<Fixture> Start(ISagaStepHandler? sagaHandler = null)
        {
            var database = await DurableSqliteDatabase.CreateAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            var services = DurableWorkflowTestSupport.CreateServices(database, DurableWorkflowTestSupport.WriteOperation("orders.manage"));
            foreach (var service in services) builder.Services.Add(service);
            builder.Services.AddBingDurableReplay(o => o.Authorize = (_, _) => ValueTask.FromResult(true),
                new ReplayHandlerRegistration("create", "1", "orders.manage", _ => new ReplayHandler()));
            builder.Services.AddBingSaga(o => o.Authorize = (_, _) => ValueTask.FromResult(true),
                new SagaHandlerRegistration("step", "1", _ => sagaHandler ?? new SagaHandler()));
            builder.Services.AddBingResilienceOperationsManagement();
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                var name = context.Request.Headers["X-Test-User"].ToString();
                if (!string.IsNullOrWhiteSpace(name)) context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, name) }, "test-only"));
                await next(context);
            });
            ResilienceOperationsEndpointOptions? captured = null;
            app.MapBingResilienceOperations(o =>
            {
                captured = o; o.EnableConsole = true;
                o.ResolveAccessScope = c => ValueTask.FromResult<string?>(c.Request.Headers["X-Test-Scope"].FirstOrDefault() ?? "tenant");
                o.AuthorizeAsync = (c, _) => ValueTask.FromResult(c.User.FindFirst(ClaimTypes.NameIdentifier)?.Value != "denied");
            });
            captured!.AuthorizeAsync = (_, _) => ValueTask.FromResult(false);
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            await app.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(new("order", "orders.manage", "v1", "create", "1", "tenant", "key", "secret-payload", DateTimeOffset.UtcNow.AddMinutes(5)));
            return new(app, database, client);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); await database.DisposeAsync(); }
    }
    private sealed class ReplayHandler : IReplayCommandHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) => ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) => ExecuteAsync(context, token);
    }
    private sealed class SagaHandler : ISagaStepHandler
    {
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Unknown));
        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) => ForwardAsync(context, token);
        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
    }
    private sealed class CountingCompensationHandler : ISagaStepHandler
    {
        private int _compensationCount;
        public int CompensationCount => Volatile.Read(ref _compensationCount);
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token)
        {
            Interlocked.Increment(ref _compensationCount);
            return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
        }
    }
}
