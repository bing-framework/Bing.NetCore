namespace Bing.Biz.FileManagement;

/// <summary>事件首次投递时冻结消费者集合；成功回执在后续重试中保留。</summary>
public sealed class FileEventDelivery
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = "";
    public int Attempts { get; set; }
    public bool Delivered { get; set; }
    public long NextAttemptAt { get; set; }
    public long CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>管理操作幂等记录及审计；请求和结果不保存签名 URL、凭据或异常原文。</summary>
public sealed class FileGovernanceAudit
{
    public Guid Id { get; set; }
    public string ScopeKey { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = "";
    public long CreatedAt { get; set; }
    public string RequestJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
}

public enum FileHistoryKind { Operation, Event, MigrationBatch }

/// <summary>逻辑归档。仅 SourceVersion 与源记录匹配时生效，不删除源记录及幂等锚点。</summary>
public sealed class FileHistoryArchive
{
    public Guid Id { get; set; }
    public string ScopeKey { get; set; } = "";
    public FileHistoryKind Kind { get; set; }
    public Guid EntityId { get; set; }
    public long SourceVersion { get; set; }
    public long ArchivedAt { get; set; }
    public string ActorId { get; set; } = "";
}

public sealed class FileHistoryCandidate
{
    public Guid Id { get; set; }
    public long Version { get; set; }
    public long Time { get; set; }
}
public sealed class FileHistorySelection
{
    public FileHistoryKind Kind { get; set; }
    public long Cutoff { get; set; }
    public IReadOnlyList<FileHistoryCandidate> Items { get; set; } = Array.Empty<FileHistoryCandidate>();
}
public sealed class FileEventQuery
{
    public string ScopeKey { get; set; } = "";
    public Guid? FileId { get; set; }
    public bool? Delivered { get; set; }
    public bool FailedOnly { get; set; }
    public bool IncludeArchived { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}
public sealed class FileEventView
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string Kind { get; set; } = "";
    public long CreatedAt { get; set; }
    public long Version { get; set; }
    public bool Delivered { get; set; }
    public long NextAttemptAt { get; set; }
    public bool Processing { get; set; }
    public string? ErrorCode { get; set; }
}
public sealed class FileGovernanceRequest
{
    public Guid RequestId { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class ReplayFileEvent
{
    public Guid RequestId { get; set; }
    public string Reason { get; set; } = "";
    public long ExpectedVersion { get; set; }
    public string Consumer { get; set; } = "";
}

public sealed partial class FileManagementOptions
{
    public int MaxEventDeliveryAttempts { get; set; } = 8;
    public int HistoryRetentionDays { get; set; } = 90;
}
