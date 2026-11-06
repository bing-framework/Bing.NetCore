using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;

namespace Bing.Biz.FileManagement;

/// <summary>空 OwnerId 表示租户/主机默认目标；个人偏好不能决定业务发布目标。</summary>
public sealed class FileStoragePreference
{
    public string Id { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Container { get; set; } = "";
    public string TargetId { get; set; } = "";
    public long Version { get; set; } = 1;
}

public enum FileBindingCopyState { Pending, Copied, Bound, Cancelled, Failed }

public sealed class FileBindingCopyRequest
{
    public Guid Id { get; set; }
    public string RequestKey { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public Guid SourceFileId { get; set; }
    public long SourceVersion { get; set; }
    public Guid CopyFileId { get; set; }
    public string BusinessType { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public FileBindingCopyState State { get; set; }
    public string? ErrorCode { get; set; }
    public long CreatedAt { get; set; }
    public long Version { get; set; } = 1;
}

/// <summary>与 FileRecord 共库的短事务；false 只表示 CAS/唯一键冲突。</summary>
public interface IFileStorageTargetStore
{
    Task<FileStoragePreference?> FindPreferenceAsync(string scopeKey, string ownerId, string container, CancellationToken ct = default);
    Task<bool> SavePreferenceAsync(FileStoragePreference preference, long expectedVersion, CancellationToken ct = default);
    Task<FileBindingCopyRequest?> FindCopyAsync(Guid id, CancellationToken ct = default);
    Task<FileBindingCopyRequest?> FindCopyRequestAsync(string requestKey, CancellationToken ct = default);
    /// <summary>同事务插入复制请求、私有目标文件及后台操作。</summary>
    Task<bool> CreateCopyAsync(FileBindingCopyRequest request, FileEntry target, FileOperation operation, CancellationToken ct = default);
    /// <summary>同事务检查请求版本、源文件版本、目标根 CAS、操作/传输租约；绑定和取消也使用此入口。</summary>
    Task<bool> UpdateCopyAsync(FileBindingCopyRequest request, long expectedVersion, FileEntry target, long targetVersion,
        IReadOnlyList<FileOperation>? operations = null, FileOperationLease? operationLease = null, FileTransferLease? transferLease = null,
        bool validateSource = false, CancellationToken ct = default);
}

public sealed class FileUploadStoreSelection
{
    public string StoreName { get; set; } = "";
    public Guid? ConnectionId { get; set; }
    public RemoteDriveOwnership Ownership { get; set; }
}

public interface IFileUploadStoreSelector
{
    Task<FileUploadStoreSelection> SelectAsync(StorageScope scope, string ownerId, string container, string? targetId,
        bool businessCopy = false, CancellationToken ct = default);
}
