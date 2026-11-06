using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchCursor_IsBoundToUserTenantAndFilters_AndSessionsAreDiscoverable(bool freeSql)
    {
        await using var app = await Harness.CreateAsync(freeSql); var client = app.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files/context")).StatusCode);
        app.SignIn();
        var context = await client.GetFromJsonAsync<FileUserContext>("/api/files/context"); Assert.Equal("user-a", context!.UserId);
        var files = new List<Guid>();
        for (var i = 0; i < 3; i++) files.Add(await app.EmptyFileAsync($"report-{i}.pdf"));
        var first = (await client.GetFromJsonAsync<JsonElement>("/api/files/search?name=report&take=1"));
        var ids = first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        var cursor = first.GetProperty("nextCursor").GetString(); Assert.NotNull(cursor);
        var url = "/api/files/search?name=report&take=1&cursor=" + Uri.EscapeDataString(cursor!);
        var second = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.DoesNotContain(second.GetProperty("items")[0].GetProperty("id").GetGuid(), ids);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url + "tamper")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url.Replace("name=report", "name=other"))).StatusCode);
        app.Env.Identity.UserId = "user-b";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
        app.Env.Identity.UserId = "user-a"; app.Env.Identity.Tenant = "tenant-b";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
        app.Env.Identity.Tenant = "tenant-a";
        var session = await app.Services.GetRequiredService<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "resume.bin", Length = 10 });
        var found = await client.GetFromJsonAsync<UploadSessionSummary[]>("/api/files/upload-sessions"); Assert.Equal(session.Id, Assert.Single(found!).Id);
        app.Env.Identity.UserId = "user-b";
        Assert.Empty((await client.GetFromJsonAsync<UploadSessionSummary[]>("/api/files/upload-sessions"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/files/upload-sessions?state=999")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdministrativeRoutes_RequirePermissionScopeAndVersion_AndHidePayload(bool freeSql)
    {
        await using var app = await Harness.CreateAsync(freeSql); app.SignIn(); var client = app.Client;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/files/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/files/migration-batches")).StatusCode);
        app.Env.Identity.Permissions.Add("files.manage");
        var fileId = await app.EmptyFileAsync("one.txt");
        var operations = await client.GetFromJsonAsync<JsonElement>("/api/files/operations?fileId=" + fileId);
        Assert.True(operations.GetArrayLength() >= 2);
        Assert.False(operations[0].TryGetProperty("payload", out _)); Assert.False(operations[0].TryGetProperty("leaseToken", out _));
        var opId = operations[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/files/operations/{opId}/audit")).StatusCode);
        var selection = new CreateMigrationBatch { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" };
        var preview = await client.PostAsJsonAsync("/api/files/migration-batches/preview", new FileManagementControllerBase.MigrationPreviewRequest { Selection = selection });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var response = await client.PostAsJsonAsync("/api/files/migration-batches", selection); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var batch = (await response.Content.ReadFromJsonAsync<FileMigrationBatch>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/files/migration-batches/{batch.Id}/pause", new { expectedVersion = batch.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/files/migration-batches/{batch.Id}/resume", new { expectedVersion = batch.Version })).StatusCode);
        var progress = (await client.GetFromJsonAsync<FileMigrationBatchView>($"/api/files/migration-batches/{batch.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/files/migration-batches/{batch.Id}/resume", new { expectedVersion = progress.Batch.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/files/diagnostics")).StatusCode);
        app.Env.Identity.Permissions.Add("files.host");
        Assert.Equal("Healthy", (await client.GetFromJsonAsync<JsonElement>("/api/files/diagnostics")).GetProperty("health").GetString());
        app.Env.Identity.Tenant = "other";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/migration-batches/{batch.Id}/items")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/operations/{opId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<FileOperationView[]>("/api/files/operations"))!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBindingCopy_HidesRetryAndRequiresANewOwnerRequestThroughHttp(bool freeSql)
    {
        await using var app = await Harness.CreateAsync(freeSql);
        app.SignIn();
        var fileId = await app.EmptyFileAsync("failed-copy.txt");
        var store = app.Services.GetRequiredService<IFileManagementStore>();
        var file = (await store.FindAsync(fileId))!;
        var operation = new FileOperation { Id = Guid.NewGuid(), FileId = fileId, Kind = FileOperationKind.CopyForBinding,
            State = FileOperationState.Failed, Version = 1, LastError = "copy_error", Payload = "private-copy-request", CreatedAt = 1 };
        Assert.True(await store.TryUpdateAsync(file, file.File.Version, operations: new[] { operation }));
        var url = $"/api/files/operations/{operation.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync(url)).StatusCode);
        app.Env.Identity.Permissions.Add("files.manage");
        var view = (await app.Client.GetFromJsonAsync<FileOperationView>(url))!;
        Assert.Equal("none", view.RecoveryAction);
        var response = await app.Client.PostAsJsonAsync(url + "/retry", new RecoverFileOperation
        { RequestId = Guid.NewGuid(), ExpectedVersion = view.Version, Reason = "owner must create a new copy" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("new_copy_required", await response.Content.ReadAsStringAsync());
        Assert.Null((await store.FindOperationAsync(operation.Id, file.File.ScopeKey))!.RecoveryOperationId);
        app.Env.Identity.Tenant = "other";
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PausedMigrationBatch_ReportsCompletedItemsThroughHttp(bool freeSql)
    {
        await using var app = await Harness.CreateAsync(freeSql); app.SignIn();
        app.Env.Identity.Permissions.Add("files.manage");
        await app.EmptyFileAsync("pause-progress.txt");
        var response = await app.Client.PostAsJsonAsync("/api/files/migration-batches", new CreateMigrationBatch
        { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var batch = (await response.Content.ReadFromJsonAsync<FileMigrationBatch>())!;
        var service = app.Services.GetRequiredService<FileMigrationBatchService>();
        await service.AdvanceAsync();
        var url = $"/api/files/migration-batches/{batch.Id}";
        var before = (await app.Client.GetFromJsonAsync<FileMigrationBatchView>(url))!;
        Assert.Equal(1, before.Queued);
        Assert.Equal(HttpStatusCode.NoContent, (await app.Client.PostAsJsonAsync(url + "/pause", new { expectedVersion = before.Batch.Version })).StatusCode);
        await app.Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
        await service.AdvanceAsync();
        var after = (await app.Client.GetFromJsonAsync<FileMigrationBatchView>(url))!;
        Assert.Equal(FileMigrationBatchState.Paused, after.Batch.State);
        Assert.Equal(1, after.Succeeded); Assert.Equal(0, after.Queued);
        var item = Assert.Single((await app.Client.GetFromJsonAsync<FileMigrationBatchItem[]>(url + "/items"))!);
        Assert.Equal(FileMigrationItemState.Succeeded, item.State);
        app.Env.Identity.Tenant = "another-tenant";
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task CapacityReturns429_AndHealthDetectsMissingProbeAndStoppedWorker()
    {
        await using var app = await Harness.CreateAsync(false, o => o.Admission.TenantSessions = 1); app.SignIn();
        var request = new CreateFileUpload { RequestId = Guid.NewGuid(), FileName = "pending.bin", Length = 10 };
        Assert.Equal(HttpStatusCode.Accepted, (await app.Client.PostAsJsonAsync("/api/files/upload-sessions", request)).StatusCode);
        request.RequestId = Guid.NewGuid();
        var denied = await app.Client.PostAsJsonAsync("/api/files/upload-sessions", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode); Assert.NotNull(denied.Headers.RetryAfter);
        var options = app.Services.GetRequiredService<FileManagementHealthOptions>();
        var health = app.Services.GetRequiredService<FileManagementHealthCheck>();
        Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new())).Status);
        options.ExpectWorker = true; Assert.Equal(HealthStatus.Degraded, (await health.CheckHealthAsync(new())).Status);
        options.ExpectWorker = false; options.Probes.Add(new() { StoreName = "local-v1", TenantId = "tenant-a", RequireObject = true });
        Assert.Equal(HealthStatus.Unhealthy, (await health.CheckHealthAsync(new())).Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => health.CheckHealthAsync(new(), new CancellationToken(true)));
    }

    private sealed class Harness : IAsyncDisposable
    {
        public FileTestEnvironment Env { get; }
        private readonly TestServer _server;
        public IServiceProvider Services => _server.Services;
        public HttpClient Client { get; }
        private Harness(bool freeSql, Action<FileManagementOptions>? configure)
        {
            Env = new(freeSql) { ConfigureManagement = configure };
            _server = new(new WebHostBuilder().ConfigureServices(services =>
            {
                Env.Configure(services);
                services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HttpTestAuthentication>("test", _ => { });
                services.AddAuthorization(); services.AddControllers().AddApplicationPart(typeof(HttpTestFilesController).Assembly);
                services.AddBingFileManagementWeb(false);
            }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));
            Client = _server.CreateClient();
        }
        public static async Task<Harness> CreateAsync(bool freeSql, Action<FileManagementOptions>? configure = null)
        { var app = new Harness(freeSql, configure); await FileTestEnvironment.InitializeDatabaseAsync(app.Services); return app; }
        public void SignIn() => Client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        public async Task<Guid> EmptyFileAsync(string name)
        {
            var service = Services.GetRequiredService<FileManagementService>();
            var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = name, Length = 0 });
            await Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
            await service.CompleteUploadAsync(session.Id);
            await Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync(); return session.FileId;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); _server.Dispose(); await Env.DisposeAsync(); }
    }
}
