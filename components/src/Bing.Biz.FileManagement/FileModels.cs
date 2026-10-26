using System.Text.Json;
using Bing.FileStorage;

namespace Bing.Biz.FileManagement;

public enum FileState { Pending, Uploading, Completing, Ready, DeletePending, Deleted, Aborted, Failed, Quarantined }
public enum FileVisibility { Private, Public }
public enum FileOperationKind { Initialize, Complete, Abort, Delete, Migrate, DeleteReplica, Inspect }
/// <summary>调度类别，不持久化；分离可能长时间传输的任务与初始化/删除任务。</summary>
public enum FileOperationQueue { Short, Long }
public enum FileOperationState { Pending, Running, Succeeded, Failed }

/// <summary>独立文件表。Version 是整个文件聚合的 CAS 版本，所有业务变更均经过该根记录。</summary>
public sealed class FileRecord
{
    public Guid Id { get; set; }
    public string RequestKey { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public string? TenantId { get; set; }
    public StorageScopeKind ScopeKind { get; set; }
    public string StoreName { get; set; } = "";
    public string Container { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Length { get; set; }
    public string ContentType { get; set; } = "";
    public FileVisibility Visibility { get; set; }
    public FileState State { get; set; }
    public string CreatorId { get; set; } = "";
    public long CreatedAt { get; set; }
    public long Version { get; set; }
    public string? Sha256 { get; set; }
    /// <summary>创建时固化的内容策略；后续配置变化不能使隔离文件跳过原检查。</summary>
    public string ContentPolicyJson { get; set; } = "{}";
}

public sealed class FileBinding
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string BusinessType { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public string CreatorId { get; set; } = "";
    public long CreatedAt { get; set; }
}

/// <summary>期限与分片策略均持久化。用户刷新或配置变化不会重写现有会话策略。</summary>
public sealed class FileUploadSession
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string OwnerId { get; set; } = "";
    public string? BackendUploadId { get; set; }
    /// <summary>Provider 返回的会话元数据，必须与上传标识一起持久化以便重启对账。</summary>
    public string BackendMetadataJson { get; set; } = "{}";
    public long TotalLength { get; set; }
    public int PartSize { get; set; }
    public FileState State { get; set; }
    public FileWriteMode WriteMode { get; set; }
    public long ExpiresAt { get; set; }
    public long AbsoluteExpiresAt { get; set; }
    public long LastActivityAt { get; set; }
    public long ClientLastModified { get; set; }
    public long Version { get; set; }
}

/// <summary>预登记摘要；代理模式仅在实际上传并核对远端后保存 ETag，直传模式在完成确认时保存。</summary>
public sealed class FileUploadPart
{
    public Guid SessionId { get; set; }
    public int Number { get; set; }
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string? ETag { get; set; }
}

public sealed class FileOperation
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public FileOperationKind Kind { get; set; }
    public FileOperationState State { get; set; }
    public string Payload { get; set; } = "";
    public int Attempts { get; set; }
    public long NextAttemptAt { get; set; }
    public string? LeaseToken { get; set; }
    public long LeaseUntil { get; set; }
    public string? LastError { get; set; }
    public long Version { get; set; }
    public long CreatedAt { get; set; }
    public long FinishedAt { get; set; }
    /// <summary>人工恢复创建后继任务，原尝试历史不重置。</summary>
    public Guid? RecoveryOperationId { get; set; }
    public Guid? ParentOperationId { get; set; }
}

/// <summary>后台写入的所有权凭据；仓储必须在业务提交的同一事务中验证。</summary>
public sealed class FileOperationLease
{
    public FileOperationLease(Guid operationId, string token) { OperationId = operationId; Token = token; }
    public Guid OperationId { get; }
    public string Token { get; }
}

public enum FileTransferState { Active, Published, Retired, Cleaned }

/// <summary>
/// 复制前持久登记的独占目标。发布与文件位置在同一事务提交；回收先退休，禁止再次发布。
/// WriterCompleted 只在后端确认写入结束后设置。结果未知的退休目标保留复查记录，防止迟到写入成为无主内容。
/// </summary>
public sealed class FileTransferAttempt
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string ScopeKey { get; set; } = "";
    public StorageScopeKind ScopeKind { get; set; }
    public string? TenantId { get; set; }
    public string StoreName { get; set; } = "";
    public string Container { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string LeaseToken { get; set; } = "";
    public long LeaseUntil { get; set; }
    public long NextCheckAt { get; set; }
    public FileTransferState State { get; set; }
    public bool WriterCompleted { get; set; }
    public long Version { get; set; }
}

/// <summary>只允许本次复制执行者发布对应暂存内容。</summary>
public sealed class FileTransferLease
{
    public FileTransferLease(Guid id, string token) { Id = id; Token = token; }
    public Guid Id { get; }
    public string Token { get; }
}

public sealed class LegacyFileImport
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "";
    public Guid LegacyId { get; set; }
    public Guid FileId { get; set; }
    public string Status { get; set; } = "";
    public string? Issue { get; set; }
    public long UpdatedAt { get; set; }
}

/// <summary>仓储返回脱离 ORM 跟踪的独立快照。分片单独查询，避免每次签名复制全会话。</summary>
public sealed class FileEntry
{
    public FileRecord File { get; set; } = new();
    public FileUploadSession? Session { get; set; }
    public List<FileBinding> Bindings { get; set; } = new();
    /// <summary>仅创建时使用的准入参数；预留与文件创建同事务，不作为 FileEntry 字段映射。</summary>
    public FileUploadAdmission? Admission { get; set; }
}

public static class FileModelExtensions
{
    public static StorageScope Scope(this FileRecord file) => file.ScopeKind == StorageScopeKind.Tenant
        ? StorageScope.ForTenant(file.TenantId ?? throw new InvalidOperationException("租户位置缺少租户标识。")) : StorageScope.Host;
    public static FileAddress Address(this FileRecord file) => new(file.Container, file.ObjectKey);
    public static FileWriteRequest WriteRequest(this FileEntry entry) => new(entry.File.Address(), entry.File.Length,
        entry.Session?.WriteMode ?? FileWriteMode.CreateOnly, entry.File.ContentType,
        new Dictionary<string, string> { ["bing-session"] = entry.Session?.Id.ToString("N") ?? entry.File.Id.ToString("N") });
    public static MultipartUpload Upload(this FileEntry entry) => new(entry.Session?.BackendUploadId ??
        throw new FileManagementException("conflict", "后端上传会话尚未就绪。"), FileWriteRequest.RestoreMultipart(entry.File.Address(), entry.File.Length, entry.Session.WriteMode, entry.File.ContentType,
            JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Session.BackendMetadataJson) ?? throw new InvalidOperationException("上传元数据损坏。")), entry.Session.PartSize);
    public static FilePart ToStoragePart(this FileUploadPart part) => new() { Number = part.Number, Length = part.Length, ETag = part.ETag ?? "", Sha256 = part.Sha256 };
}
