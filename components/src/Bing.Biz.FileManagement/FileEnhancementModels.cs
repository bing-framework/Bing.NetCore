namespace Bing.Biz.FileManagement;

/// <summary>数据库候选查询；范围必须显式提供，业务权限由应用层检查。</summary>
public sealed class FileCandidateQuery
{
    public string ScopeKey { get; set; } = "";
    public string? CreatorId { get; set; }
    public string? Container { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public string? StoreName { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
    public long? CreatedFrom { get; set; }
    public long? CreatedTo { get; set; }
    public long? AfterCreatedAt { get; set; }
    public string? AfterKey { get; set; }
    public int Take { get; set; } = 50;
}

public sealed class FileSearchRequest
{
    public string? Name { get; set; }
    public string? Container { get; set; }
    public string? ContentType { get; set; }
    public long? CreatedFrom { get; set; }
    public long? CreatedTo { get; set; }
    public bool Manage { get; set; }
    public int Take { get; set; } = 50;
    public string? Cursor { get; set; }
}

/// <summary>候选扫描位置；Web 将其与身份/查询条件一起加密保护。</summary>
public sealed class FileScanPosition
{
    public long CreatedAt { get; set; }
    public string Key { get; set; } = "";
}
public sealed class FileSearchResult
{
    public IReadOnlyList<FileView> Items { get; set; } = Array.Empty<FileView>();
    public FileScanPosition? NextPosition { get; set; }
}
public sealed class UploadSessionSummary
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = "";
    public string Container { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public long LastModified { get; set; }
    public long ExpiresAt { get; set; }
    public FileState State { get; set; }
}
public sealed class FileOperationQuery
{
    public string ScopeKey { get; set; } = "";
    public Guid? FileId { get; set; }
    public FileOperationKind? Kind { get; set; }
    public FileOperationState? State { get; set; }
    public long? CreatedFrom { get; set; }
    public long? CreatedTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}
public sealed class FileOperationAudit
{
    public Guid Id { get; set; }
    public string RequestKey { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid ReplacementId { get; set; }
    public Guid FileId { get; set; }
    public string ActorId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string TraceId { get; set; } = "";
    public long CreatedAt { get; set; }
}
public sealed class RecoverFileOperation
{
    public Guid RequestId { get; set; }
    public long ExpectedVersion { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class FileOperationView
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public FileOperationKind Kind { get; set; }
    public FileOperationState State { get; set; }
    public int Attempts { get; set; }
    public long CreatedAt { get; set; }
    public long FinishedAt { get; set; }
    public long NextAttemptAt { get; set; }
    public long Version { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? RecoveryOperationId { get; set; }
    public string RecoveryAction { get; set; } = "none";
}

public enum FileMigrationBatchState { Running, Paused, Completed }
public enum FileMigrationItemState { Pending, Queued, Succeeded, Failed, Conflict }
public sealed class FileMigrationBatch
{
    public Guid Id { get; set; }
    public string RequestKey { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string SourceStore { get; set; } = "";
    public string TargetStore { get; set; } = "";
    public string? Container { get; set; }
    public FileMigrationBatchState State { get; set; }
    public long CreatedAt { get; set; }
    public long? AfterCreatedAt { get; set; }
    public string? AfterKey { get; set; }
    public bool DiscoveryCompleted { get; set; }
    public long Version { get; set; }
}
public sealed class FileMigrationBatchItem
{
    public Guid BatchId { get; set; }
    public Guid FileId { get; set; }
    public long SourceVersion { get; set; }
    public long Length { get; set; }
    public Guid? OperationId { get; set; }
    public FileMigrationItemState State { get; set; }
    public string? ErrorCode { get; set; }
    public long Version { get; set; }
}
public sealed class FileMigrationBatchView
{
    public FileMigrationBatch Batch { get; set; } = new();
    public long Total { get; set; }
    public long Bytes { get; set; }
    public long Pending { get; set; }
    public long Queued { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public long Conflicts { get; set; }
}
public sealed class CreateMigrationBatch
{
    public Guid RequestId { get; set; }
    public string SourceStore { get; set; } = "";
    public string TargetStore { get; set; } = "";
    public string? Container { get; set; }
}

public sealed class FileUploadAdmission
{
    /// <summary>0 表示不启用相应限制。</summary>
    public int TenantSessions { get; set; }
    public long TenantBytes { get; set; }
    public int UserSessions { get; set; }
    public long UserBytes { get; set; }
}
/// <summary>短事务竞争控制行；通过 Version CAS 串行化同范围的额度检查与写入。</summary>
public sealed class FileControlLock
{
    public string Id { get; set; } = "";
    public long Version { get; set; }
}
public sealed class FileUploadReservation
{
    public Guid FileId { get; set; }
    public string ScopeKey { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public long Bytes { get; set; }
    public long CreatedAt { get; set; }
}
public sealed class FileIoLease
{
    public Guid Id { get; set; }
    public string PoolKey { get; set; } = "";
    public string Token { get; set; } = "";
    public long ExpiresAt { get; set; }
}
public sealed class FileOutboxEvent
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string ScopeKey { get; set; } = "";
    public long FileVersion { get; set; }
    public string Kind { get; set; } = "";
    public long CreatedAt { get; set; }
    public long NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LeaseToken { get; set; }
    public long LeaseUntil { get; set; }
    public bool Delivered { get; set; }
    public long Version { get; set; }
    public string? ErrorCode { get; set; }
}
public sealed class FileOperationalSnapshot
{
    public long PendingOperations { get; set; }
    public long RunningOperations { get; set; }
    public long FailedOperations { get; set; }
    public long OldestDueAt { get; set; }
    public long ActiveIoLeases { get; set; }
    public long ReservedBytes { get; set; }
    public long PendingEvents { get; set; }
}
