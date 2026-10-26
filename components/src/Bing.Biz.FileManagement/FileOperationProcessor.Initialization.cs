using System.Text.Json;
using Bing.FileStorage;
using Microsoft.Extensions.Logging;

namespace Bing.Biz.FileManagement;

public sealed partial class FileOperationProcessor
{
    /// <summary>独立于文件当前所在地的补偿快照；胜出会话及后续迁移均不能改变清理目标。</summary>
    internal sealed record UnpublishedUpload(string StoreName, StorageScopeKind ScopeKind, string? TenantId,
        string Container, string ObjectKey, long Length, FileWriteMode Mode, string ContentType,
        int PartSize, string UploadId, Dictionary<string, string> Metadata)
    {
        public StorageScope Scope() => ScopeKind == StorageScopeKind.Tenant ? StorageScope.ForTenant(TenantId!) : StorageScope.Host;
        public MultipartUpload Upload() => new(UploadId,
            FileWriteRequest.RestoreMultipart(new(Container, ObjectKey), Length, Mode, ContentType, Metadata), PartSize);
        public bool IsPublishedBy(FileEntry entry) => entry.Session?.BackendUploadId == UploadId &&
            entry.File.StoreName == StoreName && entry.File.ScopeKind == ScopeKind && entry.File.TenantId == TenantId &&
            entry.File.Container == Container && entry.File.ObjectKey == ObjectKey;
    }

    private async Task TrackUnpublishedUploadAsync(FileEntry original, MultipartUpload upload, Exception failure)
    {
        var snapshot = new UnpublishedUpload(original.File.StoreName, original.File.ScopeKind, original.File.TenantId,
            upload.Request.Address.Container, upload.Request.Address.Key, upload.Request.Length, upload.Request.Mode,
            upload.Request.ContentType, upload.PartSize, upload.UploadId, new(upload.Request.Metadata));
        var cleanup = FileManagementService.Operation(original.File.Id, FileOperationKind.Abort, JsonSerializer.Serialize(snapshot));
        // 停机/HTTP 取消不能取消唯一的补偿机会；恢复窗口有界，不阻塞无限等待。
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var current = await _store.FindAsync(original.File.Id, recovery.Token)
                        ?? throw new FileManagementException("not_found", "初始化补偿的文件记录不存在。");
                    // 提交响应丢失：已发布的 UploadId 必须保留，原异常仍交由任务层记录。
                    if (snapshot.IsPublishedBy(current)) return;
                    if (await _store.FindOperationAsync(cleanup.Id, ct: recovery.Token) != null ||
                        await _store.TryUpdateAsync(current, current.File.Version, operations: new[] { cleanup }, ct: recovery.Token))
                    {
                        // 先持久化再尝试 Abort。CAS 同时阻止旧版本的迟到发布；失败任务可重试或人工恢复。
                        try { await AbortUnpublishedUploadAsync(current, cleanup.Payload, recovery.Token); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("初始化补偿任务 {OperationId} 已登记，后端中止待重试，错误代码 {Code}", cleanup.Id, ErrorCode(ex));
                        }
                        return;
                    }
                }
                catch (Exception ex) when (attempt < 2 && IsTransient(ex)) { /* 下一轮先查同一任务 ID，吸收提交响应丢失。 */ }
            }
            throw new FileManagementException("conflict", "初始化补偿登记持续竞争。");
        }
        catch (Exception trackingFailure)
        {
            // 数据库完全不可用时不能猜测提交是否成功并盲目 Abort；保留已知标识用于运维对账。
            _logger.LogError("初始化补偿登记失败，文件 {FileId}，存储 {StoreName}，上传标识 {UploadId}，错误代码 {Code}",
                original.File.Id, original.File.StoreName, upload.UploadId, ErrorCode(trackingFailure));
            throw new AggregateException("初始化提交及补偿登记失败，需恢复数据库后对账。", failure, trackingFailure);
        }
    }

    private async Task AbortUnpublishedUploadAsync(FileEntry current, string payload, CancellationToken ct)
    {
        var snapshot = JsonSerializer.Deserialize<UnpublishedUpload>(payload)
            ?? throw new InvalidOperationException("初始化补偿参数损坏。");
        if (snapshot.IsPublishedBy(current) && current.File.State is not (FileState.Aborted or FileState.Failed)) return;
        var storage = _storage.GetStorage(snapshot.StoreName, snapshot.Scope());
        await ((IMultipartFileStorage)storage).AbortMultipartAsync(snapshot.Upload(), ct);
    }
}
