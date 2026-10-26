namespace Bing.Biz.FileManagement;

public partial interface IFileManagementStore
{
    /// <summary>Ready 候选在 SQL 内筛选；CreatedAt 降序、RequestKey 升序，键集位置严格排除自身。Take 1～100。</summary>
    Task<IReadOnlyList<FileRecord>> QueryFilesAsync(FileCandidateQuery query, CancellationToken ct = default);
    /// <summary>仅数据库摘要；按 File.CreatedAt 降序、Session.Id 升序，不访问后端分片。</summary>
    Task<IReadOnlyList<UploadSessionSummary>> QuerySessionsAsync(string scopeKey, string ownerId, FileState? state, bool includeExpired, long now, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<FileOperation>> QueryOperationsAsync(FileOperationQuery query, CancellationToken ct = default);
    Task<FileOperation?> FindOperationAsync(Guid id, string? scopeKey = null, CancellationToken ct = default);
    Task<FileOperationAudit?> FindRecoveryAsync(string requestKey, CancellationToken ct = default);
    Task<IReadOnlyList<FileOperationAudit>> ListOperationAuditAsync(string scopeKey, Guid operationId, CancellationToken ct = default);
    /// <summary>原 Failed 任务 CAS + 文件版本/范围检查；原任务只能生成一个后继，审计、入队和关联批次项跟随后继原子提交。</summary>
    Task<bool> TryRecoverOperationAsync(FileOperation original, long fileVersion, FileOperation replacement, FileOperationAudit audit, CancellationToken ct = default);
    Task<FileMigrationBatch?> FindBatchAsync(Guid id, string? scopeKey = null, CancellationToken ct = default);
    Task<FileMigrationBatch?> FindBatchRequestAsync(string requestKey, CancellationToken ct = default);
    Task<bool> TryCreateBatchAsync(FileMigrationBatch batch, CancellationToken ct = default);
    Task<IReadOnlyList<FileMigrationBatch>> ListActiveBatchesAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<FileMigrationBatch>> ListBatchesAsync(string scopeKey, int skip, int take, CancellationToken ct = default);
    /// <summary>批次 CAS 更新及新增条目同事务；(BatchId,FileId) 唯一，重试不能重复加入。</summary>
    Task<bool> TryUpdateBatchAsync(FileMigrationBatch batch, long expectedVersion, IReadOnlyList<FileMigrationBatchItem>? additions = null, CancellationToken ct = default);
    Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsAsync(Guid batchId, FileMigrationItemState? state, int skip, int take, CancellationToken ct = default);
    /// <summary>在 SQL 内选择 Running/Paused 批次中 Queued 且关联操作已 Succeeded/Failed 的条目，再按批次创建时间、批次 ID、文件 ID 排序限量；排除仍在执行的操作，不占用安排批次的候选配额。Take 0～1000，返回脱离跟踪的快照。</summary>
    Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsToReconcileAsync(int take, CancellationToken ct = default);
    Task<FileMigrationBatchView> GetBatchProgressAsync(Guid batchId, CancellationToken ct = default);
    /// <summary>锁定 Running 批次，CAS Pending 条目及 Ready 文件源版本，再排队任务；文件版本递增一。</summary>
    Task<bool> TryQueueBatchItemAsync(FileMigrationBatch batch, FileMigrationBatchItem item, FileOperation operation, CancellationToken ct = default);
    Task<bool> TryUpdateBatchItemAsync(FileMigrationBatchItem item, long expectedVersion, CancellationToken ct = default);
    /// <summary>同池获取有界租约；到期槽可回收。使用控制行 CAS，不允许并发 count+insert 超发。</summary>
    Task<bool> TryAcquireIoLeaseAsync(FileIoLease lease, int limit, long now, CancellationToken ct = default);
    Task<bool> RenewIoLeaseAsync(Guid id, string token, long expiresAt, CancellationToken ct = default);
    Task ReleaseIoLeaseAsync(Guid id, string token, CancellationToken ct = default);
    Task ReleaseUploadReservationAsync(Guid fileId, CancellationToken ct = default);
    Task<IReadOnlyList<FileOutboxEvent>> ListDueEventsAsync(long now, int take, CancellationToken ct = default);
    Task<bool> TryClaimEventAsync(Guid id, long version, string token, long now, long leaseUntil, CancellationToken ct = default);
    Task<bool> RenewEventLeaseAsync(Guid id, string token, long leaseUntil, CancellationToken ct = default);
    Task<bool> FinishEventAsync(Guid id, string token, bool delivered, long nextAttemptAt, string? error, CancellationToken ct = default);
    Task<FileOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken ct = default);
}
