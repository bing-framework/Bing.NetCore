using System.Reflection;
using System.Text.Json;
using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class BatchMigrationPauseTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PausedBatch_ReconcilesTerminalOperationAfterRestart_WithoutDiscoveryOrQueueing(bool freeSql, bool fail)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var uploads = new[] { await env.UploadAsync(new byte[] { 1, 2, 3 }), await env.UploadAsync(new byte[] { 4 }), await env.UploadAsync(new byte[] { 5 }) };
        var store = env.Get<IFileManagementStore>();
        var files = new List<FileRecord>();
        for (var i = 0; i < uploads.Length; i++)
        {
            var entry = (await store.FindAsync(uploads[i].FileId))!;
            entry.File.CreatedAt = 30 - i * 10;
            Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
            files.Add((await store.FindAsync(entry.File.Id))!.File);
        }
        var service = env.Get<FileMigrationBatchService>();
        var batch = await service.CreateAsync(files[0].Scope(), "operator", new()
        { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        // 保存一个合法的中途发现检查点：首项已入队，第二项待入队，第三项尚未发现。
        var items = files.Take(2).Select(file => new FileMigrationBatchItem
        { BatchId = batch.Id, FileId = file.Id, SourceVersion = file.Version, Length = file.Length, Version = 1 }).ToArray();
        batch.AfterCreatedAt = files[1].CreatedAt; batch.AfterKey = files[1].RequestKey;
        Assert.True(await store.TryUpdateBatchAsync(batch, batch.Version, items));
        var operation = new FileOperation { Id = Guid.NewGuid(), FileId = files[0].Id, Kind = FileOperationKind.Migrate,
            Payload = JsonSerializer.Serialize(new FileMigrationService.Replica
            { StoreName = "local-v2", Container = "attachments", ExpectedVersion = files[0].Version + 1 }) };
        Assert.True(await store.TryQueueBatchItemAsync(batch, items[0], operation));
        await service.SetPausedAsync(batch.ScopeKey, batch.Id, batch.Version, true);
        if (fail)
            await env.Get<IFileStorageFactory>().GetStorage("local-v1", files[0].Scope()).DeleteAsync(files[0].Address());
        await env.RunAsync(1);
        Assert.Equal(fail ? FileOperationState.Failed : FileOperationState.Succeeded, (await store.FindOperationAsync(operation.Id))!.State);

        await env.RestartAsync(); store = env.Get<IFileManagementStore>(); service = env.Get<FileMigrationBatchService>();
        await service.AdvanceAsync();
        var progress = await service.GetAsync(batch.ScopeKey, batch.Id);
        Assert.Equal(FileMigrationBatchState.Paused, progress.Batch.State);
        Assert.False(progress.Batch.DiscoveryCompleted);
        Assert.Equal(batch.AfterCreatedAt, progress.Batch.AfterCreatedAt); Assert.Equal(batch.AfterKey, progress.Batch.AfterKey);
        Assert.Equal(2, progress.Total); Assert.Equal(1, progress.Pending); Assert.Equal(0, progress.Queued);
        Assert.Equal(fail ? 0 : 1, progress.Succeeded); Assert.Equal(fail ? 1 : 0, progress.Failed);
        var saved = await store.ListBatchItemsAsync(batch.Id, null, 0, 100);
        Assert.Equal(FileMigrationItemState.Pending, saved.Single(x => x.FileId == files[1].Id).State);
        Assert.DoesNotContain(saved, x => x.FileId == files[2].Id);
        Assert.Single(await store.QueryOperationsAsync(new() { ScopeKey = batch.ScopeKey, Kind = FileOperationKind.Migrate }));
        if (fail) Assert.NotNull(saved.Single(x => x.FileId == files[0].Id).ErrorCode);
        else
        {
            var moved = (await store.FindAsync(files[0].Id))!.File;
            Assert.Equal("local-v2", moved.StoreName);
            await env.Get<IFileStorageFactory>().GetStorage(moved.StoreName, moved.Scope()).ReadAsync(moved.Address(), async (stream, ct) =>
            { using var copy = new MemoryStream(); await stream.CopyToAsync(copy, ct); Assert.Equal(new byte[] { 1, 2, 3 }, copy.ToArray()); });
        }
        await service.AdvanceAsync();
        Assert.Equal(progress.Batch.Version, (await service.GetAsync(batch.ScopeKey, batch.Id)).Batch.Version);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Reconciliation_PreservesConcurrentPauseAndResume(bool freeSql, bool resumeDuringReconciliation)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var upload = await env.UploadAsync(new byte[] { 9 });
        var store = env.Get<IFileManagementStore>(); var service = env.Get<FileMigrationBatchService>();
        var batch = await service.CreateAsync(StorageScope.ForTenant(env.Identity.Tenant!), "operator", new()
        { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        await service.AdvanceAsync();
        batch = (await store.FindBatchAsync(batch.Id))!;
        if (resumeDuringReconciliation) await service.SetPausedAsync(batch.ScopeKey, batch.Id, batch.Version, true);
        await env.RunAsync(1); // 本次操作成功，下一次维护才有可对账结果。
        var proxy = BatchPauseStoreProxy.Wrap(store); var raced = false;
        if (resumeDuringReconciliation)
            proxy.OnItemUpdate = async next =>
            {
                if (!raced)
                {
                    raced = true; var current = (await store.FindBatchAsync(batch.Id))!;
                    await service.SetPausedAsync(batch.ScopeKey, batch.Id, current.Version, false);
                }
                return await next();
            };
        else
            proxy.OnBatchUpdate = async (candidate, next) =>
            {
                if (!raced && candidate.State == FileMigrationBatchState.Completed)
                {
                    raced = true; var current = (await store.FindBatchAsync(batch.Id))!;
                    await service.SetPausedAsync(batch.ScopeKey, batch.Id, current.Version, true);
                }
                return await next();
            };
        await new FileMigrationBatchService((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>()).AdvanceAsync();
        Assert.True(raced);
        var progress = await service.GetAsync(batch.ScopeKey, batch.Id);
        Assert.Equal(1, progress.Succeeded); Assert.Equal(0, progress.Queued);
        Assert.Equal(resumeDuringReconciliation ? FileMigrationBatchState.Completed : FileMigrationBatchState.Paused, progress.Batch.State);
        Assert.Equal("local-v2", (await store.FindAsync(upload.FileId))!.File.StoreName);
        if (!resumeDuringReconciliation)
        {
            await service.SetPausedAsync(batch.ScopeKey, batch.Id, progress.Batch.Version, false);
            await service.AdvanceAsync();
            Assert.Equal(FileMigrationBatchState.Completed, (await service.GetAsync(batch.ScopeKey, batch.Id)).Batch.State);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyPausedBatches_DoNotBlockRunningBatchScheduling(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var upload = await env.UploadAsync(new byte[] { 7 });
        var service = env.Get<FileMigrationBatchService>(); var store = env.Get<IFileManagementStore>();
        var scope = StorageScope.ForTenant(env.Identity.Tenant!);
        for (var i = 0; i < 12; i++)
        {
            var paused = await service.CreateAsync(scope, "operator", new()
            { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
            await service.SetPausedAsync(scope.Key, paused.Id, paused.Version, true);
        }
        var running = await service.CreateAsync(scope, "operator", new()
        { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        await env.RunAsync(3);
        var progress = await service.GetAsync(scope.Key, running.Id);
        Assert.Equal(FileMigrationBatchState.Completed, progress.Batch.State); Assert.Equal(1, progress.Succeeded);
        Assert.Equal("local-v2", (await store.FindAsync(upload.FileId))!.File.StoreName);
        foreach (var paused in (await store.ListBatchesAsync(scope.Key, 0, 100)).Where(x => x.Id != running.Id))
        {
            Assert.Equal(FileMigrationBatchState.Paused, paused.State);
            Assert.Empty(await store.ListBatchItemsAsync(paused.Id, null, 0, 100));
        }
    }
}

/// <summary>仅在真实仓储提交边界插入另一管理请求，验证版本竞争；不替代持久化行为。</summary>
public class BatchPauseStoreProxy : DispatchProxy
{
    private IFileManagementStore _inner = null!;
    public Func<Func<Task<bool>>, Task<bool>>? OnItemUpdate { get; set; }
    public Func<FileMigrationBatch, Func<Task<bool>>, Task<bool>>? OnBatchUpdate { get; set; }
    public static BatchPauseStoreProxy Wrap(IFileManagementStore inner)
    { var proxy = (BatchPauseStoreProxy)Create<IFileManagementStore, BatchPauseStoreProxy>(); proxy._inner = inner; return proxy; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.TryUpdateBatchItemAsync) && OnItemUpdate != null)
            return OnItemUpdate(() => (Task<bool>)method.Invoke(_inner, args)!);
        if (method.Name == nameof(IFileManagementStore.TryUpdateBatchAsync) && OnBatchUpdate != null)
            return OnBatchUpdate((FileMigrationBatch)args![0]!, () => (Task<bool>)method.Invoke(_inner, args)!);
        return method.Invoke(_inner, args);
    }
}
