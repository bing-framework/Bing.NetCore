using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDelete_RecoversOnceWithAuditAndPreservedHistory(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 1 });
        var store = env.Get<IFileManagementStore>(); var admin = env.Get<FileOperationAdministration>();
        var entry = (await store.FindAsync(session.FileId))!;
        entry.File.State = FileState.DeletePending;
        var failed = new FileOperation { Id = Guid.NewGuid(), FileId = session.FileId, Kind = FileOperationKind.Delete,
            State = FileOperationState.Failed, Attempts = 5, LastError = "storage:Unavailable", Version = 1 };
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { failed }));
        var request = new RecoverFileOperation { RequestId = Guid.NewGuid(), ExpectedVersion = 1, Reason = "storage restored" };
        var replacement = await admin.RecoverAsync(entry.File.ScopeKey, "operator", failed.Id, request);
        Assert.Equal(replacement, await admin.RecoverAsync(entry.File.ScopeKey, "operator", failed.Id, request));
        var history = (await store.FindOperationAsync(failed.Id))!;
        Assert.Equal(FileOperationState.Failed, history.State); Assert.Equal(5, history.Attempts);
        Assert.Equal(replacement, history.RecoveryOperationId);
        Assert.Equal(replacement, Assert.Single(await store.ListOperationAuditAsync(entry.File.ScopeKey, failed.Id)).ReplacementId);
        await Assert.ThrowsAsync<FileManagementException>(() => admin.RecoverAsync(entry.File.ScopeKey, "operator", failed.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = history.Version, Reason = "second recovery" }));
        await env.RestartAsync(); await env.RunAsync();
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
        Assert.Null(await env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope()).GetPropertiesAsync(entry.File.Address()));
        Assert.Empty(await env.Get<IFileManagementStore>().QueryOperationsAsync(new() { ScopeKey = StorageScope.ForTenant("other").Key }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_RejectsFailedUploadAndNeverRevivesDeletedRoot(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var upload = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "failed.bin", Length = 0 });
        var store = env.Get<IFileManagementStore>(); var entry = (await store.FindAsync(upload.FileId))!;
        entry.File.State = entry.Session!.State = FileState.Failed;
        var failed = new FileOperation { Id = Guid.NewGuid(), FileId = upload.FileId, Kind = FileOperationKind.Complete, State = FileOperationState.Failed, Version = 1 };
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { failed }));
        var admin = env.Get<FileOperationAdministration>();
        var error = await Assert.ThrowsAsync<FileManagementException>(() => admin.RecoverAsync(entry.File.ScopeKey, "actor", failed.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = 1, Reason = "recover" }));
        Assert.Equal("new_upload_required", error.Code);
        entry = (await store.FindAsync(upload.FileId))!; entry.File.State = FileState.Deleted;
        var delete = new FileOperation { Id = Guid.NewGuid(), FileId = upload.FileId, Kind = FileOperationKind.Delete, State = FileOperationState.Failed, Version = 1 };
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { delete }));
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => admin.RecoverAsync(entry.File.ScopeKey, "actor", delete.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = 1, Reason = "recover" }))).Code);
        Assert.Equal(FileState.Deleted, (await store.FindAsync(upload.FileId))!.File.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BatchMigration_PauseRestartAndIdempotentDiscovery_PreserveActualContents(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var first = await env.UploadAsync(new byte[] { 1, 2, 3 });
        var second = await env.UploadAsync(new byte[] { 4, 5 });
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);
        var request = new CreateMigrationBatch { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" };
        var service = env.Get<FileMigrationBatchService>(); var store = env.Get<IFileManagementStore>();
        Assert.Equal(2, (await service.PreviewAsync(scope, request)).Count);
        var batch = await service.CreateAsync(scope, "operator", request);
        Assert.Equal(batch.Id, (await service.CreateAsync(scope, "operator", request)).Id);
        await service.SetPausedAsync(scope.Key, batch.Id, batch.Version, true);
        await service.AdvanceAsync();
        Assert.Empty(await store.ListBatchItemsAsync(batch.Id, null, 0, 100));
        await env.RestartAsync(); service = env.Get<FileMigrationBatchService>(); store = env.Get<IFileManagementStore>();
        batch = (await store.FindBatchAsync(batch.Id))!;
        await service.SetPausedAsync(scope.Key, batch.Id, batch.Version, false);
        await env.RunAsync(8);
        var progress = await service.GetAsync(scope.Key, batch.Id);
        Assert.Equal(FileMigrationBatchState.Completed, progress.Batch.State);
        Assert.Equal(2, progress.Total); Assert.Equal(2, progress.Succeeded); Assert.Equal(5, progress.Bytes);
        foreach (var (id, expected) in new[] { (first.FileId, new byte[] { 1, 2, 3 }), (second.FileId, new byte[] { 4, 5 }) })
        {
            var file = (await store.FindAsync(id))!.File; Assert.Equal("local-v2", file.StoreName);
            await env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope()).ReadAsync(file.Address(), async (stream, ct) =>
            { using var copy = new MemoryStream(); await stream.CopyToAsync(copy, ct); Assert.Equal(expected, copy.ToArray()); });
        }
        await service.AdvanceAsync(); Assert.Equal(2, (await service.GetAsync(scope.Key, batch.Id)).Succeeded);
        await Assert.ThrowsAsync<FileManagementException>(() => service.GetAsync(StorageScope.ForTenant("other").Key, batch.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BatchQueue_RejectsConcurrentDeletionWithoutResurrection(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 3 }); var store = env.Get<IFileManagementStore>();
        var batch = await env.Get<FileMigrationBatchService>().CreateAsync(StorageScope.ForTenant(env.Identity.Tenant!), "actor",
            new() { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        var file = (await store.FindAsync(session.FileId))!.File;
        var item = new FileMigrationBatchItem { BatchId = batch.Id, FileId = file.Id, SourceVersion = file.Version, Length = file.Length, Version = 1 };
        batch.DiscoveryCompleted = true; Assert.True(await store.TryUpdateBatchAsync(batch, batch.Version, new[] { item }));
        await env.Get<FileManagementService>().DeleteAsync(file.Id);
        await env.RunAsync(5);
        Assert.Equal(FileState.Deleted, (await store.FindAsync(file.Id))!.File.State);
        Assert.Equal(1, (await env.Get<FileMigrationBatchService>().GetAsync(batch.ScopeKey, batch.Id)).Conflicts);
        Assert.Empty((await store.QueryOperationsAsync(new() { ScopeKey = batch.ScopeKey, Kind = FileOperationKind.Migrate })));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BatchRecovery_AtomicallyFollowsSuccessorEvenWhenCallerStopsBeforeUpdatingItem(bool freeSql, bool paused)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 9 }); var store = env.Get<IFileManagementStore>();
        var root = (await store.FindAsync(session.FileId))!;
        var failed = new FileOperation { Id = Guid.NewGuid(), FileId = session.FileId, Kind = FileOperationKind.Migrate,
            State = FileOperationState.Failed, Version = 1, Attempts = 5,
            Payload = System.Text.Json.JsonSerializer.Serialize(new FileMigrationService.Replica { StoreName = "local-v2", Container = root.File.Container, ExpectedVersion = root.File.Version }) };
        Assert.True(await store.TryUpdateAsync(root, root.File.Version, operations: new[] { failed }));
        var batch = await env.Get<FileMigrationBatchService>().CreateAsync(root.File.Scope(), "operator",
            new() { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        batch.DiscoveryCompleted = true; batch.State = paused ? FileMigrationBatchState.Paused : FileMigrationBatchState.Completed;
        Assert.True(await store.TryUpdateBatchAsync(batch, batch.Version, new[] { new FileMigrationBatchItem
        { BatchId = batch.Id, FileId = session.FileId, SourceVersion = root.File.Version, Length = 1, OperationId = failed.Id, State = FileMigrationItemState.Failed, ErrorCode = "old-error", Version = 1 } }));
        var replacement = await env.Get<FileOperationAdministration>().RecoverAsync(root.File.ScopeKey, "operator", failed.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = 1, Reason = "restored target" });
        // 没有执行任何客户端批次修补动作，直接模拟进程重启。
        await env.RestartAsync(); store = env.Get<IFileManagementStore>();
        var item = Assert.Single(await store.ListBatchItemsAsync(batch.Id, null, 0, 100));
        Assert.Equal(replacement, item.OperationId); Assert.Equal(FileMigrationItemState.Queued, item.State); Assert.Null(item.ErrorCode);
        var current = (await store.FindBatchAsync(batch.Id))!;
        Assert.Equal(paused ? FileMigrationBatchState.Paused : FileMigrationBatchState.Running, current.State);
        if (paused) await env.Get<FileMigrationBatchService>().SetPausedAsync(batch.ScopeKey, batch.Id, current.Version, false);
        await env.RunAsync(5);
        Assert.Equal(1, (await env.Get<FileMigrationBatchService>().GetAsync(batch.ScopeKey, batch.Id)).Succeeded);
        Assert.Equal("local-v2", (await store.FindAsync(session.FileId))!.File.StoreName);
    }
}
