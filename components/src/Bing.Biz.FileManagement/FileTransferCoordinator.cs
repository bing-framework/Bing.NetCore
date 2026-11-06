using Bing.FileStorage;
using Microsoft.Extensions.Logging;

namespace Bing.Biz.FileManagement;

/// <summary>复制前持久登记独立目标；发布与退休互斥，未知写入结果持续对账。</summary>
internal sealed class FileTransferCoordinator
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly FileManagementOptions _options;
    private readonly ILogger _logger;
    public FileTransferCoordinator(IFileManagementStore store, IFileStorageFactory storage, FileManagementOptions options, ILogger logger)
    { _store = store; _storage = storage; _options = options; _logger = logger; }

    public async Task RunAsync(FileRecord file, string targetStore, string container,
        Func<FileTransferAttempt, FileTransferLease, CancellationToken, Task> copyAndPublish, CancellationToken ct)
    {
        var until = FileManagementService.Now + (long)_options.OperationLease.TotalMilliseconds;
        var attempt = new FileTransferAttempt
        {
            Id = Guid.NewGuid(), FileId = file.Id, ScopeKey = file.ScopeKey, ScopeKind = file.ScopeKind, TenantId = file.TenantId,
            StoreName = targetStore, Container = container, ObjectKey = Guid.NewGuid().ToString("N"),
            LeaseToken = Guid.NewGuid().ToString("N"), LeaseUntil = until, NextCheckAt = until, Version = 1
        };
        await _store.CreateTransferAsync(attempt, ct);
        var lease = new FileTransferLease(attempt.Id, attempt.LeaseToken);
        using var work = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var stop = new CancellationTokenSource();
        var heartbeat = RenewAsync(lease, work, stop.Token);
        try { await copyAndPublish(attempt, lease, work.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && work.IsCancellationRequested)
        {
            // 内部停止必须进入持久任务的有限重试；外部请求取消仍保留取消语义。
            throw await heartbeat ?? new FileStorageException(FileStorageError.Conflict, "复制传输租约已变化，请重试。");
        }
        finally
        {
            stop.Cancel(); await heartbeat;
            // 请求取消也要尝试短时收尾；故障时由已登记位置继续恢复，不丢失副本地址。
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await TryCleanupAsync(attempt.Id, attempt.LeaseToken, cleanup.Token);
        }
    }
    private async Task<FileStorageException?> RenewAsync(FileTransferLease lease, CancellationTokenSource work, CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.OperationLease.TotalMilliseconds / 3), stop);
                if (!await _store.RenewTransferAsync(lease, FileManagementService.Now + (long)_options.OperationLease.TotalMilliseconds, stop))
                {
                    work.Cancel();
                    return new FileStorageException(FileStorageError.Conflict, "复制传输租约已变化，请重试。");
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            work.Cancel();
            return new FileStorageException(FileStorageError.Unavailable, "复制传输续租暂时不可用。", ex);
        }
        return null;
    }
    public async Task CleanupAsync(CancellationToken ct)
    {
        foreach (var attempt in await _store.ListTransfersToCleanAsync(FileManagementService.Now, 50, ct))
        {
            ct.ThrowIfCancellationRequested();
            await TryCleanupAsync(attempt.Id, null, ct);
        }
    }
    private async Task TryCleanupAsync(Guid id, string? token, CancellationToken ct)
    {
        try
        {
            var retired = await _store.RetireTransferAsync(id, token, ct);
            if (retired == null) return; // Published 也包含数据库已提交但调用方未收到响应的情况。
            var scope = retired.ScopeKind == StorageScopeKind.Host ? StorageScope.Host : StorageScope.ForTenant(retired.TenantId!);
            await _storage.GetStorage(retired.StoreName, scope).DeleteAsync(new FileAddress(retired.Container, retired.ObjectKey), ct);
            // 未确认写入结束的退休记录保留，防止远端迟到写入发生在本次删除之后。
            await _store.FinishTransferCleanupAsync(id, retired.Version, FileManagementService.Now + (long)TimeSpan.FromHours(1).TotalMilliseconds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("复制目标 {TransferId} 回收未完成，等待重试，错误类型 {ErrorType}", id, ex.GetType().Name);
        }
    }
}
