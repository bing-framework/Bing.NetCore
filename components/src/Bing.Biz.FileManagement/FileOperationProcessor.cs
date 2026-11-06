using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bing.FileStorage;
using Microsoft.Extensions.Logging;

namespace Bing.Biz.FileManagement;

/// <summary>可重复调用的任务执行器。数据库租约控制调度，版本检查控制业务提交；长 IO 期间独立续租。</summary>
public sealed partial class FileOperationProcessor
{
    private static readonly Meter Meter = new("Bing.FileManagement", "1.0");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("bing.files.operations.completed");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("bing.files.operations.failed");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("bing.files.operations.duration", "s");
    private static readonly Counter<long> Transferred = Meter.CreateCounter<long>("bing.files.bytes", "By");
    private static long _activeSessions, _pendingCleanup, _failedOperations, _nextMetricsAt;
    static FileOperationProcessor()
    {
        Meter.CreateObservableGauge("bing.files.sessions.active", () => Interlocked.Read(ref _activeSessions));
        Meter.CreateObservableGauge("bing.files.cleanup.pending", () => Interlocked.Read(ref _pendingCleanup));
        Meter.CreateObservableGauge("bing.files.operations.failure_backlog", () => Interlocked.Read(ref _failedOperations));
    }
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly FileMigrationService _migration;
    private readonly FileTransferCoordinator _transfers;
    private readonly FileManagementOptions _options;
    private readonly ILogger<FileOperationProcessor> _logger;
    private readonly FileOutboxDispatcher? _outbox;
    private readonly Bing.FileStorage.RemoteDrive.RemoteDriveMaintenance? _remoteMaintenance;
    public FileOperationProcessor(IFileManagementStore store, IFileStorageFactory storage, FileMigrationService migration,
        FileManagementOptions options, ILogger<FileOperationProcessor> logger, FileOutboxDispatcher? outbox = null,
        Bing.FileStorage.RemoteDrive.RemoteDriveMaintenance? remoteMaintenance = null)
    {
        _store = store; _storage = storage; _migration = migration; _options = options; _logger = logger;
        _transfers = new(store, storage, options, logger);
        _outbox = outbox;
        _remoteMaintenance = remoteMaintenance;
    }
    /// <summary>顺序执行一批任务，保留 CLI 和诊断入口的计数语义；持续后台服务使用调度器。</summary>
    public async Task<int> RunBatchAsync(CancellationToken ct = default)
    {
        await RunMaintenanceAsync(ct);
        await RunEnhancementsAsync(ct);
        var due = await _store.ListDueOperationsAsync(FileManagementService.Now, 20, ct);
        var count = 0;
        foreach (var operation in due)
            if (await TryRunOperationAsync(operation, ct)) count++;
        return count;
    }

    /// <summary>只在空闲执行槽中查找并领取一项，避免任务尚在内存排队时租约已开始计时。</summary>
    internal async Task<bool> RunNextAsync(FileOperationQueue queue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var due = await _store.ListDueOperationsAsync(FileManagementService.Now, _options.StoreIoLimits.Count == 0 ? 1 : 20, queue, ct);
        // 共享槽满时有界向后查找其他存储任务，防止队首占满的存储阻塞本批其他实例。
        foreach (var operation in due)
            if (await TryRunOperationAsync(operation, ct)) return true;
        return false;
    }

    internal async Task RunMaintenanceAsync(CancellationToken ct)
    {
        if (FileManagementService.Now >= Interlocked.Read(ref _nextMetricsAt))
        {
            var statistics = await _store.GetStatisticsAsync(ct);
            Interlocked.Exchange(ref _activeSessions, statistics.ActiveSessions);
            Interlocked.Exchange(ref _pendingCleanup, statistics.PendingCleanup);
            Interlocked.Exchange(ref _failedOperations, statistics.FailedOperations);
            Interlocked.Exchange(ref _nextMetricsAt, FileManagementService.Now + 60000);
        }
        await _migration.CleanupTransfersAsync(ct);
        await ExpireSessionsAsync(ct);
        if (_remoteMaintenance != null) await _remoteMaintenance.CleanupAsync(ct);
    }

    internal async Task RunEnhancementsAsync(CancellationToken ct)
    {
        await new FileMigrationBatchService(_store, _storage).AdvanceAsync(ct);
        if (_outbox != null) await _outbox.DispatchAsync(ct);
    }

    private async Task<bool> TryRunOperationAsync(FileOperation operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = _options.StoreIoLimits.Count == 0 ? null : await _store.FindAsync(operation.FileId, ct);
        var stores = new List<string>();
        if (root != null) stores.Add(root.File.StoreName);
        if (operation.Kind is FileOperationKind.Migrate or FileOperationKind.DeleteReplica)
        {
            try { var target = JsonSerializer.Deserialize<FileMigrationService.Replica>(operation.Payload); if (target != null) stores.Add(target.StoreName); }
            catch (JsonException) { /* 由持有任务租约的执行阶段记录参数错误。 */ }
        }
        else if (operation.Kind == FileOperationKind.Abort && !string.IsNullOrEmpty(operation.Payload))
        {
            try { var cleanup = JsonSerializer.Deserialize<UnpublishedUpload>(operation.Payload); if (cleanup != null) stores.Add(cleanup.StoreName); }
            catch (JsonException) { /* 参数错误由取得租约后的执行阶段处理。 */ }
        }
        await using var permit = await new FileResourceGate(_store, _options).TryEnterAsync(stores, true, ct);
        if (permit == null) return false;
        var token = Guid.NewGuid().ToString("N");
        var now = FileManagementService.Now;
        if (!await _store.TryClaimAsync(operation.Id, operation.Version, token, now, now + (long)_options.OperationLease.TotalMilliseconds, ct))
            return false;
        using var work = CancellationTokenSource.CreateLinkedTokenSource(ct, permit.Token);
        using var renewStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = RenewAsync(operation.Id, token, work, renewStop.Token);
        var timer = Stopwatch.StartNew();
        try
        {
            await ExecuteAsync(operation, new FileOperationLease(operation.Id, token), work.Token);
            work.Token.ThrowIfCancellationRequested();
            if (await _store.FinishOperationAsync(operation.Id, token, FileOperationState.Succeeded, 0, null, ct))
                Completed.Add(1, new KeyValuePair<string, object?>("kind", operation.Kind.ToString()));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || work.IsCancellationRequested)
        {
            // 关闭或失去租约时保持可恢复 Running，其他进程在租约到期后重新对账。
            if (ct.IsCancellationRequested) throw;
        }
        catch (Exception ex)
        {
            var retry = IsTransient(ex) && operation.Attempts + 1 < _options.MaxOperationAttempts;
            var error = ErrorCode(ex); // 不保存 SDK message，防止密钥和签名 URL 进入数据库或日志。
            // 仓储原子提交任务终态、文件失败与 Abort；租约失效时不能留下业务副作用。
            if (!await _store.FinishOperationAsync(operation.Id, token, retry ? FileOperationState.Pending : FileOperationState.Failed,
                FileManagementService.Now + Math.Min(300000, 1000L << Math.Min(operation.Attempts, 8)), error, ct)) return true;
            Failures.Add(1, new KeyValuePair<string, object?>("code", error));
            _logger.LogWarning("文件任务 {OperationId} ({Kind}) 失败，错误代码 {Code}，将重试={Retry}", operation.Id, operation.Kind, error, retry);
        }
        finally
        {
            renewStop.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) when (renewStop.IsCancellationRequested) { }
            Duration.Record(timer.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("kind", operation.Kind.ToString()));
        }
        return true;
    }
    private async Task RenewAsync(Guid id, string token, CancellationTokenSource work, CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.OperationLease.TotalMilliseconds / 3), stop);
                if (!await _store.RenewLeaseAsync(id, token, FileManagementService.Now + (long)_options.OperationLease.TotalMilliseconds, stop))
                { work.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { work.Cancel(); } // 续租失败立即停止 IO，任务由租约机制恢复；不能在未持有租约时提交。
    }
    private static bool IsTransient(Exception ex) => ex is IOException || ex is TimeoutException || ex is System.Data.Common.DbException ||
        ex.InnerException is System.Data.Common.DbException ||
        ex is FileStorageException storage && storage.Error is FileStorageError.OutcomeUnknown or FileStorageError.Unavailable or FileStorageError.Conflict or FileStorageError.InvalidPart ||
        ex is FileManagementException management && management.Code == "conflict";
    private static string ErrorCode(Exception ex) => ex is FileStorageException storage ? "storage:" + storage.Error :
        ex is FileManagementException management ? "files:" + management.Code : ex.GetType().Name;
    private async Task ExecuteAsync(FileOperation operation, FileOperationLease lease, CancellationToken ct)
    {
        var entry = await _store.FindAsync(operation.FileId, ct) ?? throw new FileManagementException("not_found", "任务文件不存在。");
        var backend = _storage.GetStorage(entry.File.StoreName, entry.File.Scope());
        switch (operation.Kind)
        {
            case FileOperationKind.Initialize:
                if (entry.File.State != FileState.Pending) return;
                var upload = entry.File.Length == 0
                    ? new MultipartUpload("empty", entry.WriteRequest(), entry.Session!.PartSize)
                    : await ((IMultipartFileStorage)backend).CreateMultipartAsync(entry.WriteRequest(), entry.Session!.PartSize, ct);
                try
                {
                    entry.Session.BackendUploadId = upload.UploadId;
                    entry.Session.BackendMetadataJson = JsonSerializer.Serialize(upload.Request.Metadata);
                    entry.File.State = entry.Session.State = FileState.Uploading;
                    if (!await _store.TryUpdateAsync(entry, entry.File.Version, ct: ct, lease: lease))
                        throw new FileManagementException("conflict", "初始化版本变化。");
                }
                catch (Exception failure)
                {
                    if (entry.File.Length != 0) await TrackUnpublishedUploadAsync(entry, upload, failure);
                    throw;
                }
                return;
            case FileOperationKind.Complete:
                if (entry.File.State is FileState.Ready or FileState.Quarantined) return;
                if (entry.File.State != FileState.Completing) return;
                var properties = await backend.GetPropertiesAsync(entry.File.Address(), ct);
                if (!Matches(entry, properties))
                {
                    if (entry.File.Length == 0)
                    {
                        await CompleteEmptyAsync(entry, backend, lease, ct);
                        return;
                    }
                    // 重新查询实际分片，可吸收冻结之前已签发的同摘要迟到分片及其新 ETag。
                    var parts = await FileManagementService.ConfirmPartsAsync(entry, _store, backend, ct);
                    var result = await ((IMultipartFileStorage)backend).CompleteMultipartAsync(entry.Upload(), parts.Select(p => p.ToStoragePart()).ToArray(), ct);
                    properties = result.Properties;
                    if (!Matches(entry, properties)) throw new FileStorageException(FileStorageError.OutcomeUnknown, "完成对象属性不能确认。");
                }
                else if (entry.File.Length != 0 && backend.Capabilities.HasFlag(FileStorageCapabilities.RequiresCompletionCleanup))
                {
                    // Local 原子提交后可能尚未来得及持久化清单或删除分片，必须执行幂等收尾。
                    var multipart = (IMultipartFileStorage)backend;
                    var parts = await multipart.ListPartsAsync(entry.Upload(), ct);
                    await multipart.CompleteMultipartAsync(entry.Upload(), parts, ct);
                }
                ct.ThrowIfCancellationRequested();
                await PublishCompletedAsync(entry, lease, ct);
                Transferred.Add(entry.File.Length, new KeyValuePair<string, object?>("store", backend.Name));
                return;
            case FileOperationKind.Abort:
                if (!string.IsNullOrEmpty(operation.Payload))
                {
                    await AbortUnpublishedUploadAsync(entry, operation.Payload, ct);
                    return;
                }
                if (entry.File.State is not (FileState.Aborted or FileState.Failed)) return;
                if (entry.File.Length != 0 && entry.Session?.BackendUploadId != null) await ((IMultipartFileStorage)backend).AbortMultipartAsync(entry.Upload(), ct);
                // 仅回收本会话确认生成的失败对象。
                var failed = await backend.GetPropertiesAsync(entry.File.Address(), ct);
                if (Matches(entry, failed)) await backend.DeleteAsync(entry.File.Address(), ct);
                await _store.ReleaseUploadReservationAsync(entry.File.Id, ct);
                return;
            case FileOperationKind.Inspect:
                if (entry.File.State != FileState.Quarantined) return;
                await new FileContentInspector(_options).InspectAsync(entry.File, backend, ct);
                ct.ThrowIfCancellationRequested();
                entry.File.State = FileState.Ready;
                if (entry.Session != null) entry.Session.State = FileState.Ready;
                if (!await _store.TryUpdateAsync(entry, entry.File.Version, ct: ct, lease: lease)) throw new FileManagementException("conflict", "内容检查提交竞争。");
                return;
            case FileOperationKind.Delete:
                if (entry.File.State == FileState.Deleted) return;
                if (entry.File.State != FileState.DeletePending || entry.Bindings.Count != 0) throw new FileManagementException("conflict", "删除条件已变化。");
                await backend.DeleteAsync(entry.File.Address(), ct);
                entry.File.State = FileState.Deleted;
                if (!await _store.TryUpdateAsync(entry, entry.File.Version, ct: ct, lease: lease)) throw new FileManagementException("conflict", "删除提交竞争。");
                return;
            case FileOperationKind.Migrate:
                await _migration.ExecuteMigrationAsync(operation, lease, ct); return;
            case FileOperationKind.CopyForBinding:
                await FileBindingCopyService.ExecuteAsync(_store, _storage, _options, operation, lease, ct); return;
            case FileOperationKind.DeleteReplica:
                await _migration.DeleteReplicaAsync(operation, ct); return;
            default: throw new InvalidOperationException("未知文件任务。");
        }
    }
    private async Task CompleteEmptyAsync(FileEntry entry, IFileStorage backend, FileOperationLease lease, CancellationToken ct)
    {
        // 空文件没有后端分片会话约束迟到写入，必须先登记独立目标，不能复用共享对象键。
        await _transfers.RunAsync(entry.File, entry.File.StoreName, entry.File.Container, async (attempt, transfer, token) =>
        {
            entry.File.ObjectKey = attempt.ObjectKey;
            await backend.SaveAsync(entry.WriteRequest(), Stream.Null, token);
            await _store.MarkTransferWrittenAsync(transfer, token);
            if (!Matches(entry, await backend.GetPropertiesAsync(entry.File.Address(), token)))
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "空文件对象属性不能确认。");
            token.ThrowIfCancellationRequested();
            // 发布独立目标、隔离/Ready 状态和检查任务与租约校验在同一事务提交。
            await PublishCompletedAsync(entry, lease, token, transfer);
        }, ct);
    }
    private async Task PublishCompletedAsync(FileEntry entry, FileOperationLease lease, CancellationToken ct, FileTransferLease? transfer = null)
    {
        var inspect = FileContentInspector.RequiresInspection(entry.File);
        entry.File.State = inspect ? FileState.Quarantined : FileState.Ready;
        if (entry.Session != null) entry.Session.State = entry.File.State;
        var operations = inspect ? new[] { FileManagementService.Operation(entry.File.Id, FileOperationKind.Inspect) } : null;
        if (!await _store.TryUpdateAsync(entry, entry.File.Version, operations: operations, ct: ct, lease: lease, transfer: transfer))
            throw new FileManagementException("conflict", "完成版本或任务租约变化。");
    }
    private static bool Matches(FileEntry entry, FileProperties? properties) => properties != null && properties.Length == entry.File.Length &&
        properties.Metadata.TryGetValue("bing-session", out var session) && session == (entry.Session?.Id ?? entry.File.Id).ToString("N");
    private async Task ExpireSessionsAsync(CancellationToken ct)
    {
        foreach (var id in await _store.ListExpiredSessionsAsync(FileManagementService.Now, 50, ct))
        {
            var entry = await _store.FindSessionAsync(id, ct);
            if (entry?.Session == null || entry.File.State is not (FileState.Pending or FileState.Uploading) ||
                (entry.Session.ExpiresAt > FileManagementService.Now && entry.Session.AbsoluteExpiresAt > FileManagementService.Now)) continue;
            entry.File.State = entry.Session.State = FileState.Aborted;
            await _store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { FileManagementService.Operation(entry.File.Id, FileOperationKind.Abort) }, ct: ct);
        }
    }
}
