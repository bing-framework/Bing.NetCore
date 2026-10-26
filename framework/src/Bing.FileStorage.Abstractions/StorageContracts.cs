namespace Bing.FileStorage;

/// <summary>绑定存储实例和隔离范围的流式存储门面。</summary>
public interface IFileStorage
{
    string Name { get; }
    StorageScope Scope { get; }
    FileStorageCapabilities Capabilities { get; }
    Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default);
    /// <summary>等待消费完成后释放读取资源；回调异常原样传播，流不得逃逸回调。</summary>
    Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default);
    /// <summary>仅对象缺失返回 null；Bucket 缺失和权限错误抛出异常。</summary>
    Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default);
    Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default);
}

/// <summary>命名后端的单例拥有者；绑定出的门面不拥有共享客户端。</summary>
public interface IFileStorageBackend
{
    string Name { get; }
    IFileStorage Bind(StorageScope scope);
}

public interface IFileStorageFactory
{
    string DefaultStore { get; }
    IFileStorage GetStorage(string storeName, StorageScope scope);
}

public interface IMultipartFileStorage
{
    Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default);
    Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default);
    Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default);
    Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default);
}

public interface ISignedFileRequestProvider
{
    Task<SignedFileRequest> SignDownloadAsync(FileAddress address, TimeSpan lifetime, string? fileName = null, CancellationToken cancellationToken = default);
    Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

public interface IFileRangeReader
{
    Task ReadRangeAsync(FileAddress address, long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default);
}

public enum FileStorageError
{
    NotFound, AlreadyExists, AccessDenied, Configuration, InvalidLength, InvalidChecksum,
    InvalidPart, UploadNotFound, Conflict, CapacityExceeded, UnsupportedCapability, Unavailable, OutcomeUnknown
}

/// <summary>稳定的后端错误分类；不得将网络或授权失败伪装成对象缺失。</summary>
public sealed class FileStorageException : Exception
{
    public FileStorageException(FileStorageError error, string message, Exception? inner = null, string? requestId = null) : base(message, inner)
    { Error = error; RequestId = requestId; }
    public FileStorageError Error { get; }
    public string? RequestId { get; }
}
