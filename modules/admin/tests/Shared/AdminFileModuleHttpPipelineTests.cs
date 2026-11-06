#nullable enable

using System.Net;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Bing.Admin.Apis.Commons;
using Bing.AspNetCore.ExceptionHandling;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.ExceptionHandling;
using Bing.Tracing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>通过真实 Admin 模块验证授权中间件与文件 API 响应契约。</summary>
public sealed class AdminFileModuleHttpPipelineTests
{
    [Fact]
    public async Task RealAdminModule_EnforcesAuthorization_PreservesFileResponses_AndKeepsConsoleAnonymous()
    {
        await using var environment = new AdminFileHttpEnvironment();
        var module = new Bing.Admin.Modules.AppModule();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auditing:DatabaseEnabled"] = "false",
            ["RateLimiting:Enabled"] = "false",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused;"
        }).Build();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                new Bing.AspNetCore.AspNetCoreModule().AddServices(services);
                environment.Configure(services);
                services.AddTransient<Bing.Clients.ICurrentClient, Bing.Clients.CurrentClient>();
                services.AddSingleton<ICorrelationIdProvider, DefaultCorrelationIdProvider>();
                services.AddSingleton<IExceptionNotifier>(NullExceptionNotifier.Instance);
                services.AddTransient<IExceptionToErrorInfoConverter, DefaultExceptionToErrorInfoConverter>();
                services.AddTransient<IHttpExceptionStatusCodeFinder, DefaultHttpExceptionStatusCodeFinder>();
                services.AddTransient<IBingAuthorizationExceptionHandler, DefaultBingAuthorizationExceptionHandler>();
                services.AddSingleton<IFilePermissionChecker, PipelinePermissionChecker>();
                services.AddSingleton(new FileManagementConsoleOptions { Enabled = true });
                module.AddServices(services);
                var authenticatedPolicy = new AuthorizationPolicyBuilder("admin-file-test")
                    .RequireAuthenticatedUser()
                    .Build();
                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = authenticatedPolicy;
                    options.AddPolicy("jwt", authenticatedPolicy);
                });
            })
            .Configure(module.UseModule));
        await environment.InitializeDatabaseAsync(server.Services);

        var capabilityAction = Assert.Single(server.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(FileController) && action.ActionName == "Capabilities"));
        Assert.Contains(capabilityAction.FilterDescriptors, filter => filter.Filter is AuthorizeFilter);

        using var anonymous = server.CreateClient();
        var unauthenticated = await anonymous.GetAsync("/api/files/capabilities");
        var unauthenticatedBody = await unauthenticated.Content.ReadAsStringAsync();
        Assert.True(unauthenticated.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected anonymous file API request to be rejected, got {(int)unauthenticated.StatusCode}: {unauthenticatedBody}");

        var console = await anonymous.GetAsync("/files");
        Assert.Equal(HttpStatusCode.OK, console.StatusCode);
        Assert.Equal("text/html", console.Content.Headers.ContentType!.MediaType);

        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-file-test");

        var capabilities = await client.GetAsync("/api/files/capabilities");
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
        using (var capabilitiesJson = await JsonDocument.ParseAsync(await capabilities.Content.ReadAsStreamAsync()))
        {
            Assert.True(HasProperty(capabilitiesJson.RootElement, "context"));
            Assert.True(HasProperty(capabilitiesJson.RootElement, "upload"));
            Assert.False(HasProperty(capabilitiesJson.RootElement, "code"));
        }

        var request = new CreateFileUpload
        {
            RequestId = Guid.NewGuid(),
            FileName = "pipeline-response.txt",
            ContentType = "text/plain",
            Container = "attachments",
            Length = 0
        };
        var create = await client.PostAsJsonAsync("/api/files/upload-sessions", request);
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var createBody = await create.Content.ReadAsStringAsync();
        Guid sessionId;
        Guid fileId;
        using (var createJson = JsonDocument.Parse(createBody))
        {
            Assert.True(HasProperty(createJson.RootElement, "id"));
            Assert.False(HasProperty(createJson.RootElement, "code"));
            sessionId = GetGuid(createJson.RootElement, "id");
            fileId = GetGuid(createJson.RootElement, "fileId");
        }

        await environment.RunOperationsAsync(server.Services);
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsync($"/api/files/upload-sessions/{sessionId}/complete", content: null)).StatusCode);
        await environment.RunOperationsAsync(server.Services);

        var forbidden = await client.PostAsJsonAsync($"/api/files/{fileId}/bindings",
            new FileManagementControllerBase.BindingRequest { BusinessType = "orders", BusinessId = "not-authorized" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var forbiddenJson = await JsonDocument.ParseAsync(await forbidden.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(403, GetInt32(forbiddenJson.RootElement, "status"));
            Assert.Equal("forbidden", GetString(forbiddenJson.RootElement, "code"));
            Assert.False(HasProperty(forbiddenJson.RootElement, "message"));
        }

        var missing = await client.GetAsync($"/api/files/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var missingJson = await JsonDocument.ParseAsync(await missing.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(404, GetInt32(missingJson.RootElement, "status"));
            Assert.Equal("not_found", GetString(missingJson.RootElement, "code"));
            Assert.False(HasProperty(missingJson.RootElement, "message"));
        }
    }

    private static bool HasProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.EnumerateObject()
            .Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static int GetInt32(JsonElement element, string name)
        => element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            .Value.GetInt32();

    private static string GetString(JsonElement element, string name)
        => element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            .Value.GetString()!;

    private static Guid GetGuid(JsonElement element, string name)
        => element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            .Value.GetGuid();

    private sealed class PipelinePermissionChecker : IFilePermissionChecker
    {
        public Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default)
            => Task.FromResult(permission == "files.upload");
    }
}
