using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessQueries_ExposeOnlyAuthorizedBindingsAndDiscoverOtherOwners(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 1, 2, 3 });
        var service = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        env.Authorizer.Granted = true;
        var binding = await service.BindAsync(session.FileId, "orders", "order-1");
        var entry = (await store.FindAsync(session.FileId))!;
        entry.Bindings.Add(new() { Id = Guid.NewGuid(), FileId = session.FileId, BusinessType = "secret", BusinessId = "other", CreatorId = "user-a" });
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
        env.Identity.UserId = "user-b";
        Assert.Empty(await service.ListAsync());
        Assert.Equal(session.FileId, Assert.Single(await service.ListBusinessFilesAsync("orders", "order-1")).Id);
        Assert.Equal(binding.Id, Assert.Single(await service.ListBindingsAsync(session.FileId)).Id);
        Assert.Empty(await service.ListBusinessFilesAsync("orders", "order-1", 1, 50));
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => service.ListBusinessFilesAsync("unknown", "order-1"))).Code);
        env.Identity.Tenant = "tenant-b";
        Assert.Empty(await service.ListBusinessFilesAsync("orders", "order-1"));
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => service.ListBindingsAsync(session.FileId))).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_UsesKeysetAndFiltersWithoutDuplicateOrUnauthorizedRows(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>(); var service = env.Get<FileManagementService>();
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);
        var expected = new List<Guid>();
        for (var i = 0; i < 17; i++)
        {
            var file = new FileRecord { Id = Guid.NewGuid(), RequestKey = "query-" + i.ToString("D3"), ScopeKey = scope.Key,
                TenantId = scope.TenantId, ScopeKind = scope.Kind, StoreName = "local-v1", Container = "attachments",
                FileName = i < 12 ? "合同-" + i + ".pdf" : "image.png", ContentType = i < 12 ? "application/pdf" : "image/png",
                CreatorId = env.Identity.UserId, State = FileState.Ready, CreatedAt = 1000, Version = 1 };
            var entry = new FileEntry { File = file };
            if (i < 3) entry.Bindings.Add(new() { Id = Guid.NewGuid(), FileId = file.Id, BusinessType = "unknown", BusinessId = "no", CreatorId = file.CreatorId });
            else if (i < 12) expected.Add(file.Id);
            Assert.True(await store.TryCreateAsync(entry));
        }
        var request = new FileSearchRequest { Name = "合同", ContentType = "application/pdf", Container = "attachments", CreatedFrom = 1000, CreatedTo = 1000, Take = 2 };
        var actual = new List<Guid>(); FileScanPosition? cursor = null;
        do { var page = await service.SearchAsync(request, cursor); actual.AddRange(page.Items.Select(x => x.Id)); cursor = page.NextPosition; } while (cursor != null);
        Assert.Equal(expected, actual); Assert.Equal(actual.Count, actual.Distinct().Count());
        Assert.Empty((await service.SearchAsync(new() { Name = "missing" })).Items);
        env.Identity.Tenant = "tenant-b";
        Assert.Empty((await service.SearchAsync(request)).Items);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(request, ct: new CancellationToken(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionDiscovery_IsScopedAndSurvivesRestartWithoutBackendReads(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var original = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "resume.bin", Length = 100, LastModified = 1234 });
        await env.RunAsync(); await env.RestartAsync();
        var service = env.Get<FileManagementService>();
        var found = Assert.Single(await service.ListSessionsAsync());
        Assert.Equal(original.Id, found.Id); Assert.Equal("resume.bin", found.FileName); Assert.Equal(1234, found.LastModified);
        Assert.Equal(100, found.Length); Assert.Equal(FileState.Uploading, found.State);
        Assert.Empty(await service.ListSessionsAsync(skip: 1));
        env.Identity.UserId = "other"; Assert.Empty(await service.ListSessionsAsync());
        env.Identity.UserId = "user-a"; env.Identity.Tenant = "other"; Assert.Empty(await service.ListSessionsAsync());
        env.Identity.Tenant = "tenant-a"; await service.AbortUploadAsync(original.Id);
        Assert.Empty(await service.ListSessionsAsync());
        Assert.Equal(original.Id, Assert.Single(await service.ListSessionsAsync(FileState.Aborted, true)).Id);
    }
}
