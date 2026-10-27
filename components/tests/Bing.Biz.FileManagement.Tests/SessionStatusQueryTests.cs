using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Bing.FileStorage.Qiniu.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.FileManagement.Tests;

public sealed class SessionStatusQueryTests
{
    private readonly ITestOutputHelper _output;
    public SessionStatusQueryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LightweightUploading_DoesNotReadRemoteOrDatabaseParts_AndPreservesDefault(bool freeSql)
    {
        using var sdk = new RecordingS3Client();
        await using var env = new FileTestEnvironment(freeSql);
        env.CustomizeServices = services =>
        {
            services.AddSingleton(new BingFileStorageOptions { DefaultStore = "proxy" });
            services.AddBingFileStorageBackend("proxy", _ => new S3FileStorageBackend("proxy",
                new BingS3StorageOptions { UploadMode = S3UploadMode.ServerProxy }, sdk));
        };
        await env.InitializeAsync();
        var created = await env.Get<FileManagementService>().CreateUploadAsync(new()
            { RequestId = Guid.NewGuid(), FileName = "status.bin", Length = 1 });
        await env.RunAsync();
        var proxy = DispatchProxy.Create<IFileManagementStore, StatusStoreProxy>();
        var probe = (StatusStoreProxy)(object)proxy;
        probe.Inner = env.Get<IFileManagementStore>();
        var service = new FileManagementService(proxy, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
        var status = await service.GetSessionAsync(created.Id, false);
        Assert.Equal(FileState.Uploading, status.State);
        Assert.Empty(status.Parts);
        Assert.Equal(0, sdk.ListCalls);
        Assert.Equal(0, probe.PartReads);
        await service.GetSessionAsync(created.Id);
        Assert.Equal(1, sdk.ListCalls);
        Assert.Equal(1, probe.PartReads);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSessionAsync(created.Id, false, stop.Token));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HttpStatus_IsConstantSizeFor6400Parts_AndEnforcesOwnership(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        env.ConfigureManagement = options =>
        {
            options.PartSize = 16 * 1024 * 1024;
            options.Containers["attachments"].MaxFileLength = 100L * 1024 * 1024 * 1024;
        };
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            env.Configure(services);
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HttpTestAuthentication>("test", _ => { });
            services.AddAuthorization();
            services.AddControllers().AddApplicationPart(typeof(HttpTestFilesController).Assembly);
            services.AddBingFileManagementWeb(false);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));
        await FileTestEnvironment.InitializeDatabaseAsync(server.Services);
        var service = server.Services.GetRequiredService<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "many.bin", Length = 100L * 1024 * 1024 * 1024 });
        using var client = server.CreateClient();
        var url = $"/api/files/upload-sessions/{session.Id}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url + "?includeParts=false")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        var before = await client.GetStringAsync(url + "?includeParts=false");
        // 仅构造元数据规模，不分配或传输 100 GiB 内容。
        var parts = Enumerable.Range(1, 6400).Select(number => new FileUploadPart
        { SessionId = session.Id, Number = number, Length = 16 * 1024 * 1024, ETag = "etag-" + number, Sha256 = new string('A', 44) }).ToArray();
        if (server.Services.GetService<IFreeSql>() is { } orm)
            foreach (var batch in parts.Chunk(100)) await orm.Insert(batch).ExecuteAffrowsAsync();
        else
        {
            await using var db = await server.Services.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
            db.AddRange(parts); await db.SaveChangesAsync();
        }
        var after = await client.GetStringAsync(url + "?includeParts=false");
        Assert.Equal(before, after);
        var fullResponse = await client.GetStringAsync(url);
        Assert.Equal(6400, JsonDocument.Parse(fullResponse).RootElement.GetProperty("parts").GetArrayLength());
        _output.WriteLine($"ORM={(freeSql ? "FreeSql" : "EF Core")}; lightweightBytes={System.Text.Encoding.UTF8.GetByteCount(after)}; fullBytes={System.Text.Encoding.UTF8.GetByteCount(fullResponse)}; parts=6400");
        var store = server.Services.GetRequiredService<IFileManagementStore>();
        foreach (var state in new[] { FileState.Completing, FileState.Quarantined, FileState.Ready, FileState.Aborted, FileState.Failed })
        {
            var entry = (await store.FindSessionAsync(session.Id))!;
            entry.File.State = state; entry.Session!.State = state;
            Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
            var status = (await client.GetFromJsonAsync<UploadSessionView>(url + "?includeParts=false"))!;
            Assert.Equal(state, status.State); Assert.Empty(status.Parts);
        }
        env.Identity.UserId = "user-other";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url + "?includeParts=false")).StatusCode);
        env.Identity.UserId = "user-a"; env.Identity.Tenant = "tenant-other";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url + "?includeParts=false")).StatusCode);
    }
}

public class StatusStoreProxy : DispatchProxy
{
    public IFileManagementStore Inner { get; set; } = null!;
    public int PartReads { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.ListPartsAsync)) PartReads++;
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
}
