using System.Text.Json;
using Bing.FileStorage;

namespace Bing.Biz.FileManagement;

/// <summary>批次只编排既有迁移任务；不把内容复制纳入数据库事务。</summary>
public sealed class FileMigrationBatchService
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    public FileMigrationBatchService(IFileManagementStore store, IFileStorageFactory storage) { _store = store; _storage = storage; }

    public async Task<FileMigrationBatch> CreateAsync(StorageScope scope, string actorId, CreateMigrationBatch request, CancellationToken ct = default)
    {
        var scopeKey = scope.Key;
        Validate(scopeKey, actorId, request);
        var key = FileManagementService.HashKey(scopeKey + "\n" + actorId + "\n" + request.RequestId.ToString("N"));
        var old = await _store.FindBatchRequestAsync(key, ct);
        if (old != null) return Match(old, request);
        // 空批次同样验证实例配置，不能等到第一次复制才暴露拼写错误。
        _storage.GetStorage(request.SourceStore, scope); _storage.GetStorage(request.TargetStore, scope);
        var batch = new FileMigrationBatch { Id = Guid.NewGuid(), RequestKey = key, ScopeKey = scopeKey, ActorId = actorId,
            SourceStore = request.SourceStore, TargetStore = request.TargetStore, Container = request.Container,
            CreatedAt = FileManagementService.Now, State = FileMigrationBatchState.Running, Version = 1 };
        if (await _store.TryCreateBatchAsync(batch, ct)) return batch;
        return Match(await _store.FindBatchRequestAsync(key, ct) ?? throw Conflict(), request);
    }

    public async Task<IReadOnlyList<FileRecord>> PreviewAsync(StorageScope scope, CreateMigrationBatch request,
        FileScanPosition? position = null, CancellationToken ct = default)
    {
        var scopeKey = scope.Key;
        Validate(scopeKey, "preview", request, false);
        _storage.GetStorage(request.SourceStore, scope); _storage.GetStorage(request.TargetStore, scope);
        return await _store.QueryFilesAsync(new() { ScopeKey = scopeKey, StoreName = request.SourceStore,
            Container = request.Container, AfterCreatedAt = position?.CreatedAt, AfterKey = position?.Key, Take = 100 }, ct);
    }

    public async Task<FileMigrationBatchView> GetAsync(string scopeKey, Guid id, CancellationToken ct = default)
    {
        _ = await _store.FindBatchAsync(id, scopeKey, ct) ?? throw new FileManagementException("not_found", "迁移批次不存在。");
        return await _store.GetBatchProgressAsync(id, ct);
    }
    public async Task SetPausedAsync(string scopeKey, Guid id, long expectedVersion, bool paused, CancellationToken ct = default)
    {
        var batch = await _store.FindBatchAsync(id, scopeKey, ct) ?? throw new FileManagementException("not_found", "迁移批次不存在。");
        if (batch.Version != expectedVersion || batch.State == FileMigrationBatchState.Completed) throw Conflict();
        batch.State = paused ? FileMigrationBatchState.Paused : FileMigrationBatchState.Running;
        if (!await _store.TryUpdateBatchAsync(batch, expectedVersion, ct: ct)) throw Conflict();
    }

    /// <summary>显式重试失败项；恢复审计/后继 ID 由已有任务管理协议保证，重入会跟随后继。</summary>
    public async Task<int> RetryFailedAsync(string scopeKey, string actorId, Guid id, long expectedVersion, string reason,
        FileOperationAdministration administration, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new FileManagementException("invalid", "请提供重试原因。");
        var batch = await _store.FindBatchAsync(id, scopeKey, ct) ?? throw new FileManagementException("not_found", "迁移批次不存在。");
        if (batch.Version != expectedVersion) throw Conflict();
        batch.State = FileMigrationBatchState.Running;
        if (!await _store.TryUpdateBatchAsync(batch, expectedVersion, ct: ct)) throw Conflict();
        var count = 0;
        foreach (var item in await _store.ListBatchItemsAsync(id, FileMigrationItemState.Failed, 0, 100, ct))
        {
            if (!item.OperationId.HasValue) continue;
            var operation = await _store.FindOperationAsync(item.OperationId.Value, scopeKey, ct);
            if (operation == null) continue;
            try
            {
                var next = operation.RecoveryOperationId ?? await administration.RecoverAsync(scopeKey, actorId, operation.Id,
                    new() { RequestId = new Guid(Convert.FromHexString(FileManagementService.HashKey(id + ":" + operation.Id))[..16]),
                        ExpectedVersion = operation.Version, Reason = reason }, ct);
                // 恢复事务同步推进关联批次条目；响应丢失时也不会留下已恢复任务对应的 Failed 条目。
                if (next != Guid.Empty) count++;
            }
            catch (FileManagementException ex) when (ex.Code == "conflict")
            { item.State = FileMigrationItemState.Conflict; item.ErrorCode = "source_changed"; await _store.TryUpdateBatchItemAsync(item, item.Version, ct); }
        }
        return count;
    }

    public async Task AdvanceAsync(CancellationToken ct = default)
    {
        // 暂停只停止发现和入队；已入队任务的结果仍须对账。只查询已终态的操作，
        // 避免空暂停批次或长期执行中的条目占满本轮配额。
        foreach (var group in (await _store.ListBatchItemsToReconcileAsync(100, ct)).GroupBy(x => x.BatchId))
        {
            ct.ThrowIfCancellationRequested();
            var batch = await _store.FindBatchAsync(group.Key, ct: ct);
            if (batch == null || batch.State is not (FileMigrationBatchState.Running or FileMigrationBatchState.Paused)) continue;
            foreach (var item in group) await ReconcileAsync(batch, item, ct);
            await CompleteIfFinishedAsync(batch.Id, ct);
        }

        foreach (var candidate in await _store.ListActiveBatchesAsync(10, ct))
        {
            ct.ThrowIfCancellationRequested();
            var batch = await _store.FindBatchAsync(candidate.Id, ct: ct);
            if (batch?.State != FileMigrationBatchState.Running) continue;
            if (!batch.DiscoveryCompleted)
            {
                var files = await _store.QueryFilesAsync(new() { ScopeKey = batch.ScopeKey, StoreName = batch.SourceStore,
                    Container = batch.Container, CreatedTo = batch.CreatedAt, AfterCreatedAt = batch.AfterCreatedAt, AfterKey = batch.AfterKey, Take = 100 }, ct);
                var items = files.Select(f => new FileMigrationBatchItem { BatchId = batch.Id, FileId = f.Id,
                    SourceVersion = f.Version, Length = f.Length, State = FileMigrationItemState.Pending, Version = 1 }).ToArray();
                if (files.Count > 0) { batch.AfterCreatedAt = files[^1].CreatedAt; batch.AfterKey = files[^1].RequestKey; }
                batch.DiscoveryCompleted = files.Count < 100;
                if (!await _store.TryUpdateBatchAsync(batch, batch.Version, items, ct)) continue;
                batch = (await _store.FindBatchAsync(batch.Id, ct: ct))!;
            }
            foreach (var item in await _store.ListBatchItemsAsync(batch.Id, FileMigrationItemState.Pending, 0, 20, ct))
            {
                var entry = await _store.FindAsync(item.FileId, ct);
                if (entry?.File.State != FileState.Ready || entry.File.ScopeKey != batch.ScopeKey || entry.File.StoreName != batch.SourceStore || entry.File.Version != item.SourceVersion)
                {
                    item.State = FileMigrationItemState.Conflict; item.ErrorCode = "source_changed";
                    await _store.TryUpdateBatchItemAsync(item, item.Version, ct); continue;
                }
                var operation = FileManagementService.Operation(item.FileId, FileOperationKind.Migrate, JsonSerializer.Serialize(new FileMigrationService.Replica
                { StoreName = batch.TargetStore, Container = entry.File.Container, ExpectedVersion = item.SourceVersion + 1 }));
                await _store.TryQueueBatchItemAsync(batch, item, operation, ct);
            }
            await CompleteIfFinishedAsync(batch.Id, ct);
        }
    }

    private async Task CompleteIfFinishedAsync(Guid id, CancellationToken ct)
    {
        var progress = await _store.GetBatchProgressAsync(id, ct);
        if (progress.Batch.State != FileMigrationBatchState.Running || !progress.Batch.DiscoveryCompleted || progress.Pending != 0 || progress.Queued != 0) return;
        progress.Batch.State = FileMigrationBatchState.Completed;
        // 使用刚读取的版本；并发暂停/恢复改变版本时，本轮不能覆盖新的批次状态。
        await _store.TryUpdateBatchAsync(progress.Batch, progress.Batch.Version, ct: ct);
    }
    private async Task ReconcileAsync(FileMigrationBatch batch, FileMigrationBatchItem item, CancellationToken ct)
    {
        if (!item.OperationId.HasValue) return;
        var operation = await _store.FindOperationAsync(item.OperationId.Value, batch.ScopeKey, ct);
        if (operation == null || operation.State is FileOperationState.Pending or FileOperationState.Running) return;
        if (operation.State == FileOperationState.Failed)
        {
            if (operation.RecoveryOperationId.HasValue) { item.OperationId = operation.RecoveryOperationId; item.State = FileMigrationItemState.Queued; }
            else { item.State = FileMigrationItemState.Failed; item.ErrorCode = operation.LastError; }
        }
        else
        {
            var entry = await _store.FindAsync(item.FileId, ct);
            // 原迁移遇到版本变化可幂等退出；只有目标真的发布才把批次项记为成功。
            item.State = entry?.File.State == FileState.Ready && entry.File.StoreName == batch.TargetStore
                ? FileMigrationItemState.Succeeded : FileMigrationItemState.Conflict;
            item.ErrorCode = item.State == FileMigrationItemState.Conflict ? "source_changed" : null;
        }
        await _store.TryUpdateBatchItemAsync(item, item.Version, ct);
    }
    private static void Validate(string scope, string actor, CreateMigrationBatch request, bool requireId = true)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || requireId && request.RequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.SourceStore) || string.IsNullOrWhiteSpace(request.TargetStore) || request.SourceStore == request.TargetStore ||
            request.SourceStore.Length > 64 || request.TargetStore.Length > 64 || request.Container?.Length > 64)
            throw new FileManagementException("invalid", "迁移批次参数无效。");
    }
    private static FileMigrationBatch Match(FileMigrationBatch old, CreateMigrationBatch request)
        => old.SourceStore == request.SourceStore && old.TargetStore == request.TargetStore && old.Container == request.Container ? old : throw Conflict();
    private static FileManagementException Conflict() => new("conflict", "迁移批次或源版本已变化。");
}
