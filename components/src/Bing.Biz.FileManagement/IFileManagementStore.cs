namespace Bing.Biz.FileManagement;

/// <summary>
/// 两套 ORM 的公共事务边界。网络/磁盘传输不得放进这些方法的数据库事务。
/// 所有 FileEntry 都是脱离跟踪的快照；false 表示版本冲突，不能吞掉其他数据库异常。
/// </summary>
public partial interface IFileManagementStore
{
    /// <summary>持久化状态的监控快照，重启后仍反映真实积压量。</summary>
    Task<FileManagementStatistics> GetStatisticsAsync(CancellationToken ct = default);
    Task<FileEntry?> FindAsync(Guid fileId, CancellationToken ct = default);
    Task<FileEntry?> FindSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<FileEntry?> FindRequestAsync(string requestKey, CancellationToken ct = default);
    /// <summary>原子插入文件、可选会话及关联；主键/RequestKey 冲突返回 false。</summary>
    Task<bool> TryCreateAsync(FileEntry entry, IReadOnlyList<FileOperation>? operations = null, CancellationToken ct = default, FileTransferLease? transfer = null);
    /// <summary>
    /// 一次短事务执行 File.Version == expectedVersion 的 CAS；成功写入 expectedVersion+1。
    /// 同步更新会话（Version 跟随根版本），替换关联，按联合主键 upsert suppliedParts，插入 operations。
    /// 未提供的分片保留。CAS 失败不能写入任何子表或任务。
    /// 提供 lease 时，同时锁定并验证该文件任务的有效租约；旧执行者不得提交业务副作用。
    /// </summary>
    Task<bool> TryUpdateAsync(FileEntry entry, long expectedVersion, IReadOnlyList<FileUploadPart>? suppliedParts = null,
        IReadOnlyList<FileOperation>? operations = null, CancellationToken ct = default, FileOperationLease? lease = null, FileTransferLease? transfer = null);
    /// <summary>按会话和序号查询单个分片快照；不存在时返回 null，不加载完整清单。</summary>
    Task<FileUploadPart?> FindPartAsync(Guid sessionId, int number, CancellationToken ct = default);
    Task<IReadOnlyList<FileUploadPart>> ListPartsAsync(Guid sessionId, CancellationToken ct = default);
    /// <summary>查询明确范围的全状态记录；creatorId 为 null 的管理查询由上层显式授权。</summary>
    Task<IReadOnlyList<FileRecord>> ListFilesAsync(string scopeKey, string? creatorId, int skip, int take, CancellationToken ct = default);
    /// <summary>
    /// 在数据库内先筛选范围、可选创建者和 Ready 状态，再按 CreatedAt 降序、Id 升序分页。
    /// 返回的是待鉴权候选，skip/take 仅用于内部扫描，不代表调用者可见结果的偏移。
    /// scopeKey 不能为 null；skip/take 不能为负数，take 为 0 返回空集合；支持取消。
    /// </summary>
    Task<IReadOnlyList<FileRecord>> ListReadyFilesAsync(string scopeKey, string? creatorId, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListExpiredSessionsAsync(long now, int take, CancellationToken ct = default);
    /// <summary>返回到期 Pending 或租约过期 Running 的候选项，失败终态不自动重试。</summary>
    Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, CancellationToken ct = default);
    /// <summary>在数据库内按队列过滤后排序和限量；长队列只含 Complete/Migrate，短队列包含其余任务。take 为 0 返回空集合，负数拒绝。</summary>
    Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, FileOperationQueue queue, CancellationToken ct = default);
    /// <summary>CAS 抢占候选版本，写入 token、leaseUntil、Running，并递增 Attempts 与 Version。</summary>
    Task<bool> TryClaimAsync(Guid operationId, long expectedVersion, string token, long now, long leaseUntil, CancellationToken ct = default);
    Task<bool> RenewLeaseAsync(Guid operationId, string token, long leaseUntil, CancellationToken ct = default);
    /// <summary>
    /// 只允许仍持有有效 token 的执行者完成、重试或记为失败；清除租约并递增版本。
    /// Initialize/Complete 终止失败时，在同一事务内将对应 Pending/Completing 文件及会话置 Failed 并排队 Abort。
    /// </summary>
    Task<bool> FinishOperationAsync(Guid operationId, string token, FileOperationState state, long nextAttemptAt, string? error, CancellationToken ct = default);
    Task<LegacyFileImport?> FindImportAsync(Guid id, CancellationToken ct = default);
    Task SaveImportAsync(LegacyFileImport import, CancellationToken ct = default);
    /// <summary>必须在任何内容复制之前完成登记；位置与 Id 在整个尝试期间不可变。</summary>
    Task CreateTransferAsync(FileTransferAttempt attempt, CancellationToken ct = default);
    Task<bool> RenewTransferAsync(FileTransferLease lease, long leaseUntil, CancellationToken ct = default);
    /// <summary>仅在写入被后端确认为完成后调用；不能把超时或取消等未知结果标记为结束。</summary>
    Task MarkTransferWrittenAsync(FileTransferLease lease, CancellationToken ct = default);
    Task<IReadOnlyList<FileTransferAttempt>> ListTransfersToCleanAsync(long now, int take, CancellationToken ct = default);
    /// <summary>退休与发布互斥；token 为 null 只能退休到期目标。Published/Cleaned 返回 null，禁止删除正式对象。</summary>
    Task<FileTransferAttempt?> RetireTransferAsync(Guid id, string? token, CancellationToken ct = default);
    /// <summary>已确认写入结束的目标完成回收；未知结果保留退休记录并定时复查迟到写入。</summary>
    Task<bool> FinishTransferCleanupAsync(Guid id, long expectedVersion, long nextCheckAt, CancellationToken ct = default);
}

/// <summary>稳定的业务错误代码，由 Web 适配映射 HTTP 状态。</summary>
public sealed class FileManagementException : Exception
{
    public FileManagementException(string code, string message) : base(message) { Code = code; }
    public string Code { get; }
}

public sealed class FileManagementStatistics
{
    public long ActiveSessions { get; set; }
    public long PendingCleanup { get; set; }
    public long FailedOperations { get; set; }
}
