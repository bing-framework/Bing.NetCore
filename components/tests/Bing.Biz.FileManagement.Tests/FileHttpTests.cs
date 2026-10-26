using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Bing.FileStorage.Qiniu.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileHttpTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealHttp_Upload_Download_Range_Signing_AndAuthorization(bool freeSql, bool proxy)
    {
        using var sdk = new RecordingS3Client();
        await using var env = new FileTestEnvironment(freeSql);
        if (proxy) env.CustomizeServices = services =>
        {
            services.AddSingleton(new BingFileStorageOptions { DefaultStore = "qiniu-proxy" });
            services.AddBingFileStorageBackend("qiniu-proxy", _ => new S3FileStorageBackend("qiniu-proxy", new BingS3StorageOptions
            { UploadMode = S3UploadMode.ServerProxy, ContainerBuckets = new() { ["attachments"] = "actual-bucket" } }, sdk));
        };
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            env.Configure(services);
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HttpTestAuthentication>("test", _ => { });
            services.AddAuthorization(); services.AddControllers().AddApplicationPart(typeof(HttpTestFilesController).Assembly);
            services.AddBingFileManagementWeb(false);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));
        await FileTestEnvironment.InitializeDatabaseAsync(server.Services);
        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        var createdResponse = await client.PostAsJsonAsync("/api/files/upload-sessions", new CreateFileUpload
        { RequestId = Guid.NewGuid(), FileName = "中文 文件.txt", ContentType = "text/plain", Length = 5, LastModified = 100 });
        Assert.Equal(HttpStatusCode.Accepted, createdResponse.StatusCode);
        var session = (await createdResponse.Content.ReadFromJsonAsync<UploadSessionView>())!;
        await server.Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var partResponse = await client.PostAsJsonAsync($"/api/files/upload-sessions/{session.Id}/part-requests",
            new PartUploadRequest { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/files/upload-sessions/{session.Id}/parts/1", new ByteArrayContent(bytes))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/files/upload-sessions/{session.Id}/complete", null)).StatusCode);
        await server.Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
        FileBinding? binding = null;
        if (proxy)
        {
            env.Authorizer.Granted = true;
            var bound = await client.PostAsJsonAsync($"/api/files/{session.FileId}/bindings", new { BusinessType = "orders", BusinessId = "order-1" });
            Assert.Equal(HttpStatusCode.OK, bound.StatusCode);
            binding = (await bound.Content.ReadFromJsonAsync<FileBinding>())!;
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/files/{session.FileId}")).StatusCode);
        }
        var download = await client.GetAsync($"/api/files/{session.FileId}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("中文 文件.txt", download.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition.DispositionType);
        var range = new HttpRequestMessage(HttpMethod.Get, $"/api/files/{session.FileId}/content");
        range.Headers.Range = new(1, 3);
        var partial = await client.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal("bytes 1-3/5", partial.Content.Headers.ContentRange!.ToString());
        Assert.Equal(new byte[] { 2, 3, 4 }, await partial.Content.ReadAsByteArrayAsync());
        var badRange = new HttpRequestMessage(HttpMethod.Get, $"/api/files/{session.FileId}/content");
        badRange.Headers.TryAddWithoutValidation("Range", "bytes=0-1,3-4");
        var invalid = await client.SendAsync(badRange);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, invalid.StatusCode);
        Assert.Equal("bytes */5", invalid.Content.Headers.ContentRange!.ToString());
        var link = (await (await client.PostAsync($"/api/files/{session.FileId}/download-link", null)).Content.ReadFromJsonAsync<Link>())!;
        client.DefaultRequestHeaders.Authorization = null;
        if (proxy)
        {
            // 外部 S3 链接在 Provider 云端测试中验证；TestServer 仅验证签名参数和桶映射。
            var signed = new Uri(link.Url);
            Assert.Equal("https", signed.Scheme);
            Assert.Contains("actual-bucket", signed.AbsolutePath);
            Assert.Contains("X-Amz-Signature=", signed.Query);
        }
        else Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(link.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/public/{session.FileId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/{session.FileId}/signed-content?token=bad")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        env.Identity.Tenant = "other";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/{session.FileId}/content")).StatusCode);
        env.Identity.Tenant = "tenant-a";
        if (binding != null)
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/files/{session.FileId}/bindings/{binding.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync($"/api/files/{session.FileId}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        if (proxy)
        {
            await server.Services.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
            Assert.Empty(sdk.Objects);
        }
        else Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(link.Url)).StatusCode);
    }
    private sealed class Link { public string Url { get; set; } = ""; }
}
public sealed class HttpTestFilesController : FileManagementControllerBase
{
    public HttpTestFilesController(FileManagementService files, IFileManagementStore store, IFileStorageFactory storage,
        FileManagementOptions options, IDataProtectionProvider protection) : base(files, store, storage, options, protection) { }
}
public sealed class HttpTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public HttpTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(Request.Headers.Authorization == "Bearer test" ? AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-a") }, Scheme.Name)), Scheme.Name)) : AuthenticateResult.NoResult());
}
