using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileGovernanceHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GovernanceRoutes_EnforcePermissionScopePreviewIntegrity_AndAuditReplay(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        using var server = Server(env, true); var services = server.Services;
        await FileTestEnvironment.InitializeDatabaseAsync(services);
        using var client = server.CreateClient();
        foreach (var path in new[] { "events", "history", "governance-audit" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files/" + path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/files/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/files/history/preview", new { kind = 0 })).StatusCode);
        env.Identity.Permissions.Add("files.manage");
        var files = services.GetRequiredService<FileManagementService>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "治理测试.txt", Length = 0 });
        await services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
        await files.CompleteUploadAsync(session.Id); await services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
        await services.GetRequiredService<FileOutboxDispatcher>().DispatchAsync();
        await FileGovernanceTests.MutateAsync<FileOutboxEvent>(services, x => x.CreatedAt = DateTimeOffset.UtcNow.AddDays(-100).ToUnixTimeMilliseconds());
        var evt = Assert.Single((await client.GetFromJsonAsync<FileEventView[]>("/api/files/events"))!);
        var details = await client.GetFromJsonAsync<JsonElement>("/api/files/events/" + evt.Id);
        Assert.False(details.GetProperty("event").TryGetProperty("leaseToken", out _));
        var previewResponse = await client.PostAsJsonAsync("/api/files/history/preview", new { kind = 1 });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(preview.GetProperty("selection").GetProperty("items").EnumerateArray());
        var token = preview.GetProperty("token").GetString();
        var requestId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/files/history/archive", new { token = token + "bad", requestId, reason = "test" })).StatusCode);
        env.Identity.UserId = "other";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/files/history/archive", new { token, requestId, reason = "test" })).StatusCode);
        env.Identity.UserId = "user-a"; env.Identity.Tenant = "other";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/files/events/" + evt.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/files/history/archive", new { token, requestId, reason = "test" })).StatusCode);
        env.Identity.Tenant = "tenant-a";
        var archive = await client.PostAsJsonAsync("/api/files/history/archive", new { token, requestId, reason = "test" });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/files/history/archive", new { token, requestId, reason = "test" })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<FileEventView[]>("/api/files/events"))!);
        var marker = Assert.Single((await client.GetFromJsonAsync<FileHistoryArchive[]>("/api/files/history"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/files/history/" + marker.Id + "/restore", new { requestId = Guid.NewGuid(), reason = "restore" })).StatusCode);
        var replay = new ReplayFileEvent { RequestId = Guid.NewGuid(), ExpectedVersion = evt.Version, Consumer = "http-audit", Reason = "repair" };
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/files/events/" + evt.Id + "/replay", replay)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/files/events/" + evt.Id + "/replay", replay)).StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<FileGovernanceAudit[]>("/api/files/governance-audit"))!.Length);
        env.Identity.Tenant = "other";
        Assert.Empty((await client.GetFromJsonAsync<FileGovernanceAudit[]>("/api/files/governance-audit"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/files/events?take=101")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Console_IsOptIn_AssetsAreEmbeddedAndRestricted(bool enabled)
    {
        await using var env = new FileTestEnvironment(); using var server = Server(env, enabled); using var client = server.CreateClient();
        var page = await client.GetAsync("/files");
        Assert.Equal(enabled ? HttpStatusCode.OK : HttpStatusCode.NotFound, page.StatusCode);
        if (!enabled) return;
        Assert.Equal("nosniff", Assert.Single(page.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(page.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("文件管理", await page.Content.ReadAsStringAsync());
        Assert.Contains("type=\"password\"", await page.Content.ReadAsStringAsync());
        foreach (var asset in new[] { "console.js", "console.css", "uploader.js" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/files/" + asset)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/files/appsettings.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files/capabilities")).StatusCode);
    }

    private static TestServer Server(FileTestEnvironment env, bool console)
        => new(new WebHostBuilder().ConfigureServices(services =>
        {
            env.Configure(services);
            services.AddSingleton<IFileLifecycleEventHandler>(new FileGovernanceTests.Handler("http-audit"));
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HttpTestAuthentication>("test", _ => { });
            services.AddAuthorization(); services.AddControllers().AddApplicationPart(typeof(HttpTestFilesController).Assembly);
            services.AddBingFileManagementWeb(false, configureConsole: options => options.Enabled = console);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));
}
