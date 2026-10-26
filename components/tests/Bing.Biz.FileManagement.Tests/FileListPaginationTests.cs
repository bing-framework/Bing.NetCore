using System.Net;
using System.Net.Http.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>使用真实 SQLite 验证可见结果分页，覆盖状态、业务授权和 HTTP 数组契约。</summary>
public sealed class FileListPaginationTests
{
    [Theory]
    [InlineData(false, FileState.Aborted)] [InlineData(true, FileState.Aborted)]
    [InlineData(false, FileState.Deleted)] [InlineData(true, FileState.Deleted)]
    [InlineData(false, FileState.Failed)] [InlineData(true, FileState.Failed)]
    public async Task List_TerminalRecordsDoNotHideUploadedFile(bool freeSql, FileState hiddenState)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var uploaded = await env.UploadAsync(new byte[] { 1, 2, 3 });
        var store = env.Get<IFileManagementStore>();
        var ready = (await store.FindAsync(uploaded.FileId))!;
        ready.File.CreatedAt = 1;
        Assert.True(await store.TryUpdateAsync(ready, ready.File.Version));
        for (var i = 0; i < 50; i++)
            await FileListTestData.AddAsync(store, hiddenState, 100 + i);

        var service = env.Get<FileManagementService>();
        Assert.Equal(uploaded.FileId, Assert.Single(await service.ListAsync()).Id);
        Assert.Empty(await service.ListAsync(1, 50));
        Assert.Equal(FileState.Ready, (await service.GetAsync(uploaded.FileId)).State);
        await env.Get<IFileStorageFactory>().GetStorage(ready.File.StoreName, ready.File.Scope())
            .ReadAsync(ready.File.Address(), async (stream, ct) =>
            {
                using var content = new MemoryStream();
                await stream.CopyToAsync(content, ct);
                Assert.Equal(new byte[] { 1, 2, 3 }, content.ToArray());
            });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task List_VisibleOffsetCrossesDeniedBatchesWithoutGapsOrDuplicates(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        var expected = new List<Guid>();
        // 前 100 个候选全部拒绝；随后可见项分布在多个扫描批次中，不能按空批次提前停止。
        for (var i = 0; i < 230; i++)
        {
            var allowed = i is 105 or 151 or 229;
            var entry = await FileListTestData.AddAsync(store, FileState.Ready, 1000 - i,
                businessType: allowed ? "orders" : "unknown");
            if (allowed) expected.Add(entry.File.Id);
        }
        var service = env.Get<FileManagementService>();
        var first = await service.ListAsync(0, 2);
        var second = await service.ListAsync(2, 2);
        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Equal(expected, first.Concat(second).Select(x => x.Id));
        Assert.Equal(expected.Count, first.Concat(second).Select(x => x.Id).Distinct().Count());
        Assert.Empty(await service.ListAsync(3, 2));
        // 参数兼容：负 skip 视作 0，take 至少为 1。
        Assert.Equal(expected[0], Assert.Single(await service.ListAsync(-1, 0)).Id);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task List_PreservesCreatorTenantAndExplicitManageBoundaries(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        var own = await FileListTestData.AddAsync(store, FileState.Ready, 1);
        var denied = await FileListTestData.AddAsync(store, FileState.Ready, 2, businessType: "unknown");
        var otherOwner = await FileListTestData.AddAsync(store, FileState.Ready, 3, creatorId: "user-b");
        var otherTenant = await FileListTestData.AddAsync(store, FileState.Ready, 4, tenantId: "tenant-b");
        var service = env.Get<FileManagementService>();
        Assert.Equal(own.File.Id, Assert.Single(await service.ListAsync()).Id);
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => service.ListAsync(manage: true))).Code);

        env.Identity.Permissions.Add("files.manage");
        Assert.Equal(new[] { denied.File.Id, own.File.Id }, (await service.ListAsync()).Select(x => x.Id));
        Assert.Equal(new[] { otherOwner.File.Id, denied.File.Id }, (await service.ListAsync(0, 2, manage: true)).Select(x => x.Id));
        Assert.Equal(own.File.Id, Assert.Single(await service.ListAsync(2, 2, manage: true)).Id);
        Assert.Empty(await service.ListAsync(3, 2, manage: true));
        env.Identity.Tenant = "tenant-b";
        Assert.Equal(otherTenant.File.Id, Assert.Single(await service.ListAsync(manage: true)).Id);
        env.Identity.IsAuthenticated = false;
        Assert.Equal("unauthenticated", (await Assert.ThrowsAsync<FileManagementException>(() => service.ListAsync())).Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task List_PropagatesCancellationAndUnexpectedAuthorizationFailures(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        for (var i = 0; i < 120; i++)
            await FileListTestData.AddAsync(store, FileState.Ready, i, businessType: "orders");
        using var stop = new CancellationTokenSource();
        var checks = 0;
        var authorizer = new FileListTestAuthorizer((_, ct) =>
        {
            Assert.Equal(stop.Token, ct);
            if (++checks == 105) stop.Cancel();
            return Task.FromResult(false);
        });
        var service = CreateService(env, authorizer);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(ct: stop.Token));
        Assert.Equal(105, checks);
        var failed = CreateService(env, new FileListTestAuthorizer((_, _) => throw new IOException("authorization unavailable")));
        await Assert.ThrowsAsync<IOException>(() => failed.ListAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HttpList_KeepsArrayContractAndUsesVisibleOffsets(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            env.Configure(services);
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HttpTestAuthentication>("test", _ => { });
            services.AddAuthorization();
            services.AddControllers().AddApplicationPart(typeof(HttpTestFilesController).Assembly);
            services.AddBingFileManagementWeb(false);
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(e => e.MapControllers());
        }));
        await FileTestEnvironment.InitializeDatabaseAsync(server.Services);
        var store = server.Services.GetRequiredService<IFileManagementStore>();
        var first = await FileListTestData.AddAsync(store, FileState.Ready, 2);
        var second = await FileListTestData.AddAsync(store, FileState.Ready, 1);
        for (var i = 0; i < 50; i++)
        {
            await FileListTestData.AddAsync(store, FileState.Aborted, 100 + i);
            await FileListTestData.AddAsync(store, FileState.Ready, 100 + i, businessType: "unknown");
        }
        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "test");
        // 与浏览器示例相同的无参数请求仍可直接反序列化为数组。
        var all = await client.GetFromJsonAsync<FileView[]>("/api/files");
        Assert.Equal(new[] { first.File.Id, second.File.Id }, all!.Select(x => x.Id));
        Assert.Equal(first.File.Id, Assert.Single((await client.GetFromJsonAsync<FileView[]>("/api/files?skip=0&take=1"))!).Id);
        Assert.Equal(second.File.Id, Assert.Single((await client.GetFromJsonAsync<FileView[]>("/api/files?skip=1&take=1"))!).Id);
        Assert.Empty((await client.GetFromJsonAsync<FileView[]>("/api/files?skip=2&take=1"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/files?manage=true")).StatusCode);
        env.Identity.Tenant = "tenant-b";
        Assert.Empty((await client.GetFromJsonAsync<FileView[]>("/api/files"))!);
    }

    private static FileManagementService CreateService(FileTestEnvironment env, IFileAccessAuthorizer authorizer)
        => new(env.Get<IFileManagementStore>(), env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), authorizer, env.Get<FileManagementOptions>());
}

internal sealed class FileListTestAuthorizer : IFileAccessAuthorizer
{
    private readonly Func<string, CancellationToken, Task<bool>> _authorize;
    internal FileListTestAuthorizer(Func<string, CancellationToken, Task<bool>> authorize) { _authorize = authorize; }
    public Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access, CancellationToken ct = default)
        => _authorize(businessId, ct);
}

internal static class FileListTestData
{
    internal static async Task<FileEntry> AddAsync(IFileManagementStore store, FileState state, long createdAt,
        string creatorId = "user-a", string tenantId = "tenant-a", string? businessType = null)
    {
        var id = Guid.NewGuid();
        var scope = StorageScope.ForTenant(tenantId);
        var entry = new FileEntry
        {
            File = new FileRecord
            {
                Id = id, RequestKey = id.ToString("N"), ScopeKey = scope.Key, ScopeKind = scope.Kind, TenantId = tenantId,
                StoreName = "local-v1", Container = "attachments", ObjectKey = id.ToString("N"), FileName = "分页测试.txt",
                ContentType = "text/plain", Length = 1, State = state, CreatorId = creatorId, CreatedAt = createdAt, Version = 1
            }
        };
        if (businessType != null)
            entry.Bindings.Add(new FileBinding
            {
                Id = Guid.NewGuid(), FileId = id, BusinessType = businessType, BusinessId = id.ToString("N"),
                CreatorId = creatorId, CreatedAt = createdAt
            });
        Assert.True(await store.TryCreateAsync(entry));
        return entry;
    }
}
