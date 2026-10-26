using System.Security.Cryptography;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class AttachmentLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_Restart_RepeatComplete_AndDelete(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var uploaded = await env.UploadAsync(new byte[] { 1, 2, 3 });
        var service = env.Get<FileManagementService>();
        Assert.Equal(FileState.Ready, (await service.CompleteUploadAsync(uploaded.Id)).State);
        Assert.Equal(3, (await service.GetAsync(uploaded.FileId)).Length);
        var entry = await env.Get<IFileManagementStore>().FindAsync(uploaded.FileId);
        Assert.Equal("local-v1", entry!.File.StoreName);
        Assert.Equal(0, (await env.Get<IFileManagementStore>().GetStatisticsAsync()).ActiveSessions);
        await service.DeleteAsync(uploaded.FileId); await service.DeleteAsync(uploaded.FileId);
        Assert.Equal(1, (await env.Get<IFileManagementStore>().GetStatisticsAsync()).PendingCleanup);
        await env.RunAsync();
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(uploaded.FileId))!.File.State);
        Assert.Null(await env.Get<IFileStorageFactory>().GetStorage("local-v1", entry.File.Scope()).GetPropertiesAsync(entry.File.Address()));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resume_UsesBackendParts_AndRejectsDifferentContent(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var request = new CreateFileUpload { RequestId = Guid.NewGuid(), FileName = "resume.bin", Length = 3, LastModified = 1 };
        var session = await service.CreateUploadAsync(request);
        Assert.Equal(session.Id, (await service.CreateUploadAsync(request)).Id);
        request.Length++;
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => service.CreateUploadAsync(request))).Code);
        await env.RunAsync();
        var hash = Convert.ToBase64String(SHA256.HashData(new byte[] { 1, 2, 3 }));
        await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = hash });
        Assert.Empty((await service.GetSessionAsync(session.Id)).Parts);
        await service.UploadPartAsync(session.Id, 1, new MemoryStream(new byte[] { 1, 2, 3 }));
        // 新服务实例只依赖数据库与持久目录。
        var resumed = env.Get<FileManagementService>();
        Assert.Equal(hash, Assert.Single((await resumed.GetSessionAsync(session.Id)).Parts).Sha256);
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => resumed.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(new byte[32]) }))).Code);
        await resumed.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await resumed.GetSessionAsync(session.Id)).State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossTenant_AndSessionOwner_AreIsolated(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var uploaded = await env.UploadAsync(new byte[] { 1 });
        var service = env.Get<FileManagementService>();
        env.Identity.UserId = "user-b";
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => service.GetSessionAsync(uploaded.Id))).Code);
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => service.GetAsync(uploaded.FileId))).Code);
        env.Identity.UserId = "user-a"; env.Identity.Tenant = "tenant-b"; env.Identity.Permissions.Add("files.manage");
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => service.GetAsync(uploaded.FileId))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => service.DeleteAsync(uploaded.FileId))).Code);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Binding_DefaultDeny_ProtectsDelete_AndBoundRead(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 1 });
        var service = env.Get<FileManagementService>();
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => service.BindAsync(session.FileId, "orders", "1"))).Code);
        env.Authorizer.Granted = true;
        var binding = await service.BindAsync(session.FileId, "orders", "1");
        Assert.Equal(binding.Id, (await service.BindAsync(session.FileId, "orders", "1")).Id);
        Assert.Equal("bound", (await Assert.ThrowsAsync<FileManagementException>(() => service.DeleteAsync(session.FileId))).Code);
        env.Authorizer.Granted = false;
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => service.GetAsync(session.FileId))).Code);
        env.Authorizer.Granted = true; await service.UnbindAsync(session.FileId, binding.Id);
        await service.DeleteAsync(session.FileId); await env.RunAsync();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicFiles_RequirePublish_AndSnapshotVisibility(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        env.Identity.Permissions.Remove("files.publish");
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileManagementService>().CreateUploadAsync(
            new() { RequestId = Guid.NewGuid(), FileName = "public.txt", Container = "public" }))).Code);
        env.Identity.Permissions.Add("files.publish");
        var publicFile = await env.UploadAsync(Array.Empty<byte>(), "public");
        var privateFile = await env.UploadAsync(Array.Empty<byte>());
        env.Get<FileManagementOptions>().Containers["attachments"].Visibility = FileVisibility.Public;
        env.Identity.IsAuthenticated = false;
        Assert.Equal(publicFile.FileId, (await env.Get<FileManagementService>().AuthorizeReadAsync(publicFile.FileId, true)).File.Id);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileManagementService>().AuthorizeReadAsync(privateFile.FileId, true))).Code);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredSession_CleansParts_ButReadyUnboundSurvives(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var ready = await env.UploadAsync(new byte[] { 9 });
        var pending = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "expire", Length = 1 });
        await env.RunAsync();
        var store = env.Get<IFileManagementStore>();
        var entry = (await store.FindSessionAsync(pending.Id))!;
        entry.Session!.ExpiresAt = 0;
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
        await env.RunAsync();
        Assert.Equal(FileState.Aborted, (await store.FindSessionAsync(pending.Id))!.File.State);
        Assert.Equal(FileState.Ready, (await store.FindAsync(ready.FileId))!.File.State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionResponseLost_ReconcilesByLocationAndSessionMetadata(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "empty", Length = 0 });
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
        var entry = (await env.Get<IFileManagementStore>().FindSessionAsync(session.Id))!;
        var backend = env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope());
        // 模拟后端已完成，但业务任务尚未收到响应或更新数据库。
        await backend.SaveAsync(entry.WriteRequest(), Stream.Null);
        await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_VerifiesContent_ChangesLocation_AndRetainsDeletionState(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 1, 2, 3 });
        await env.Get<FileMigrationService>().QueueMigrationAsync(session.FileId, "local-v2");
        await env.RunAsync();
        var entry = (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!;
        Assert.Equal("local-v2", entry.File.StoreName);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(new byte[] { 1, 2, 3 })), entry.File.Sha256);
        var other = await env.UploadAsync(new byte[] { 9 });
        await env.Get<FileMigrationService>().QueueMigrationAsync(other.FileId, "local-v2");
        await env.Get<FileManagementService>().DeleteAsync(other.FileId);
        await env.RunAsync();
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(other.FileId))!.File.State);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyImport_Previews_InvalidAddress_AndStableGuid(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var request = new LegacyImportRequest { Source = "old", LegacyId = Guid.NewGuid(), RootPath = env.Root, RelativePath = "Test",
            OwnerId = "owner", Scope = StorageScope.ForTenant("tenant-a"), StoreName = "local-v1", FileName = "旧文件.txt", ExpectedLength = 3 };
        var service = env.Get<FileMigrationService>();
        Assert.Equal("NeedsAttention", (await service.ImportAsync(request, false)).Status);
        await File.WriteAllBytesAsync(Path.Combine(env.Root, "legacy.bin"), new byte[] { 4, 5, 6 });
        request.RelativePath = "legacy.bin";
        Assert.Equal("Preview", (await service.ImportAsync(request, false)).Status);
        Assert.Null(await env.Get<IFileManagementStore>().FindAsync(request.LegacyId));
        var result = await service.ImportAsync(request, true);
        Assert.Equal("Imported", result.Status); Assert.Equal(request.LegacyId, result.FileId);
        Assert.Equal(result.Id, (await service.ImportAsync(request, true)).Id);
    }
}
