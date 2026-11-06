namespace Bing.FileStorage;

/// <summary>
/// 绑定存储实例和隔离范围的流式存储门面。
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// 获取存储实例名称。
    /// </summary>
    string Name { get; }
    /// <summary>
    /// 获取存储隔离范围。
    /// </summary>
    StorageScope Scope { get; }
    /// <summary>
    /// 获取存储实例支持的能力。
    /// </summary>
    FileStorageCapabilities Capabilities { get; }
    /// <summary>
    /// 异步写入文件内容。
    /// </summary>
    /// <param name="request">待处理的请求。</param>
    /// <param name="source">待读取的文件内容流。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>文件写入后的地址及结果。</returns>
    Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步读取文件并在回调中消费内容。
    /// </summary>
    /// <remarks>等待消费完成后释放读取资源；回调异常原样传播，流不得逃逸回调。</remarks>
    /// <param name="address">目标文件地址。</param>
    /// <param name="consume">在读取资源释放前消费流的异步回调。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步获取文件属性。
    /// </summary>
    /// <remarks>仅对象缺失返回 null；Bucket 缺失和权限错误抛出异常。</remarks>
    /// <param name="address">目标文件地址。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>文件属性；仅对象不存在时为 null。</returns>
    Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步删除文件。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default);
}

/// <summary>
/// 命名后端的单例拥有者。
/// </summary>
/// <remarks>绑定出的门面不拥有共享客户端。</remarks>
public interface IFileStorageBackend
{
    /// <summary>
    /// 获取后端实例名称。
    /// </summary>
    string Name { get; }
    /// <summary>
    /// 创建绑定隔离范围的存储门面。
    /// </summary>
    /// <param name="scope">操作所属的隔离范围。</param>
    /// <returns>绑定目标范围的文件存储门面。</returns>
    IFileStorage Bind(StorageScope scope);
}

/// <summary>
/// 按名称和隔离范围创建文件存储门面。
/// </summary>
public interface IFileStorageFactory
{
    /// <summary>
    /// 获取默认存储实例名称。
    /// </summary>
    string DefaultStore { get; }
    /// <summary>
    /// 获取指定名称和隔离范围的文件存储。
    /// </summary>
    /// <param name="storeName">命名存储实例名称。</param>
    /// <param name="scope">操作所属的隔离范围。</param>
    /// <returns>绑定目标范围的文件存储门面。</returns>
    IFileStorage GetStorage(string storeName, StorageScope scope);
}

/// <summary>
/// 提供文件分片上传能力。
/// </summary>
public interface IMultipartFileStorage
{
    /// <summary>
    /// 异步创建分片上传会话。
    /// </summary>
    /// <param name="request">待处理的请求。</param>
    /// <param name="partSize">分片大小，单位为字节。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>新建的分片上传会话。</returns>
    Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步上传指定分片。
    /// </summary>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="partNumber">分片序号。</param>
    /// <param name="source">待读取的文件内容流。</param>
    /// <param name="sha256">分片内容的 SHA-256 校验值。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>已上传分片的回执。</returns>
    Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步列出已上传分片。
    /// </summary>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>当前已上传分片列表。</returns>
    Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步合并分片并完成文件上传。
    /// </summary>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="parts">用于完成上传的分片回执列表。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>文件写入后的地址及结果。</returns>
    Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步终止分片上传。
    /// </summary>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供有时限的文件访问签名。
/// </summary>
public interface ISignedFileRequestProvider
{
    /// <summary>
    /// 异步生成文件下载签名。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <param name="lifetime">签名有效时长。</param>
    /// <param name="fileName">下载时建议使用的文件名。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>包含签名地址和请求信息的访问请求。</returns>
    Task<SignedFileRequest> SignDownloadAsync(FileAddress address, TimeSpan lifetime, string? fileName = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 异步生成指定上传分片的签名。
    /// </summary>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="partNumber">分片序号。</param>
    /// <param name="sha256">分片内容的 SHA-256 校验值。</param>
    /// <param name="lifetime">签名有效时长。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>包含签名地址和请求信息的访问请求。</returns>
    Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供文件指定字节范围的流式读取能力。
/// </summary>
public interface IFileRangeReader
{
    /// <summary>
    /// 异步读取指定字节范围并在回调中消费内容。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <param name="offset">读取起始字节偏移。</param>
    /// <param name="length">读取的字节数。</param>
    /// <param name="consume">在读取资源释放前消费流的异步回调。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    Task ReadRangeAsync(FileAddress address, long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default);
}

/// <summary>
/// 标识文件存储操作失败的稳定错误类别。
/// </summary>
public enum FileStorageError
{
    /// <summary>
    /// 目标文件或上传会话不存在。
    /// </summary>
    NotFound,
    /// <summary>
    /// 仅创建写入时目标对象已存在。
    /// </summary>
    AlreadyExists,
    /// <summary>
    /// 当前身份无权执行该存储操作。
    /// </summary>
    AccessDenied,
    /// <summary>
    /// 存储后端或请求配置无效。
    /// </summary>
    Configuration,
    /// <summary>
    /// 文件或分片长度不符合请求要求。
    /// </summary>
    InvalidLength,
    /// <summary>
    /// 内容摘要校验失败。
    /// </summary>
    InvalidChecksum,
    /// <summary>
    /// 分片编号、内容或摘要不符合请求要求。
    /// </summary>
    InvalidPart,
    /// <summary>
    /// 分片上传会话不存在或已失效。
    /// </summary>
    UploadNotFound,
    /// <summary>
    /// 操作与资源当前状态冲突。
    /// </summary>
    Conflict,
    /// <summary>
    /// 请求超出后端支持的容量上限。
    /// </summary>
    CapacityExceeded,
    /// <summary>
    /// 后端不支持请求所需的存储能力。
    /// </summary>
    UnsupportedCapability,
    /// <summary>
    /// 存储后端当前不可用。
    /// </summary>
    Unavailable,
    /// <summary>
    /// 远端操作结果无法确认，可能已经产生副作用。
    /// </summary>
    OutcomeUnknown
}

/// <summary>
/// 表示文件存储后端失败的异常。
/// </summary>
/// <remarks>
/// 稳定的后端错误分类；不得将网络或授权失败伪装成对象缺失。
/// </remarks>
public sealed class FileStorageException : Exception
{
    /// <summary>
    /// 初始化一个 <see cref="FileStorageException" /> 类型的实例。
    /// </summary>
    /// <param name="error">表示操作失败类别的错误代码。</param>
    /// <param name="message">描述错误原因的消息。</param>
    /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
    /// <param name="requestId">关联当前请求的标识。</param>
    public FileStorageException(FileStorageError error, string message, Exception? inner = null, string? requestId = null) : base(message, inner)
    { Error = error; RequestId = requestId; }
    /// <summary>
    /// 获取文件存储失败的错误类别。
    /// </summary>
    public FileStorageError Error { get; }
    /// <summary>
    /// 获取后端返回的请求标识。
    /// </summary>
    public string? RequestId { get; }
}
