using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 标识远端云盘的账户归属范围。
/// </summary>
public enum RemoteDriveOwnership
{
    /// <summary>
    /// 连接到组织或团队共享的云盘。
    /// </summary>
    Shared,
    /// <summary>
    /// 连接到当前授权用户的个人云盘。
    /// </summary>
    Personal
}
/// <summary>
/// 标识远端云盘连接的授权状态。
/// </summary>
public enum RemoteDriveConnectionState
{
    /// <summary>
    /// 连接正在执行首次授权或重新授权。
    /// </summary>
    Authorizing,
    /// <summary>
    /// 连接凭据有效，可访问远端云盘。
    /// </summary>
    Active,
    /// <summary>
    /// 凭据失效，需要用户重新授权。
    /// </summary>
    ReauthorizationRequired,
    /// <summary>
    /// 连接已停用，不参与远端访问。
    /// </summary>
    Disabled
}
/// <summary>
/// 标识远端文件传输及其检查点的持久化阶段。
/// </summary>
public enum RemoteDriveTransferState
{
    /// <summary>
    /// 传输记录已创建，尚未开始接收内容。
    /// </summary>
    Initializing,
    /// <summary>
    /// 本地中转端正在接收并登记上传分片。
    /// </summary>
    Receiving,
    /// <summary>
    /// 分片清单已冻结，后续不再接受新分片。
    /// </summary>
    Frozen,
    /// <summary>
    /// 中转内容正在提交到远端云盘。
    /// </summary>
    Uploading,
    /// <summary>
    /// 远端对象已校验，等待完成记录和清理。
    /// </summary>
    Verified,
    /// <summary>
    /// 远端写入已完成，传输记录已收尾。
    /// </summary>
    Completed,
    /// <summary>
    /// 传输已中止，不再接受分片或完成操作。
    /// </summary>
    Aborted
}

/// <summary>
/// 网盘连接的持久化身份与授权状态。
/// </summary>
/// <remarks>连接身份和位置固定；只允许更新授权状态、加密凭据与租约。</remarks>
public sealed class RemoteDriveConnection
{
    /// <summary>
    /// 获取或设置网盘连接标识。
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 获取或设置已注册的网盘模板名称。
    /// </summary>
    public string TemplateName { get; set; } = "";
    /// <summary>
    /// 获取或设置后端提供程序名称。
    /// </summary>
    public string Provider { get; set; } = "";
    /// <summary>
    /// 获取或设置命名文件存储实例。
    /// </summary>
    public string StoreName { get; set; } = "";
    /// <summary>
    /// 获取或设置文件存储的隔离范围种类。
    /// </summary>
    public StorageScopeKind ScopeKind { get; set; }
    /// <summary>
    /// 获取或设置所属租户标识。
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// 获取或设置持久化存储的隔离范围键。
    /// </summary>
    public string ScopeKey { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘连接的归属类型。
    /// </summary>
    public RemoteDriveOwnership Ownership { get; set; }
    /// <summary>
    /// 获取或设置网盘连接的归属主体标识。
    /// </summary>
    public string OwnerId { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘连接的显示名称。
    /// </summary>
    public string DisplayName { get; set; } = "";
    /// <summary>
    /// 获取或设置远端网盘账户标识。
    /// </summary>
    public string RemoteAccountId { get; set; } = "";
    /// <summary>
    /// 获取或设置远端网盘磁盘标识。
    /// </summary>
    public string DriveId { get; set; } = "";
    /// <summary>
    /// 获取或设置允许操作的网盘根目录标识。
    /// </summary>
    public string RootFolderId { get; set; } = "root";
    /// <summary>
    /// 获取或设置加密后的网盘访问与刷新凭据。
    /// </summary>
    public string EncryptedCredentials { get; set; } = "";
    /// <summary>
    /// 获取或设置加密后的临时授权流程状态。
    /// </summary>
    public string EncryptedAuthorization { get; set; } = "";
    /// <summary>
    /// 获取或设置授权流程到期的 Unix 毫秒时间戳。
    /// </summary>
    public long AuthorizationExpiresAt { get; set; }
    /// <summary>
    /// 获取或设置访问凭据到期的 Unix 毫秒时间戳。
    /// </summary>
    public long CredentialExpiresAt { get; set; }
    /// <summary>
    /// 获取或设置网盘连接的授权与可用状态。
    /// </summary>
    public RemoteDriveConnectionState State { get; set; }
    /// <summary>
    /// 获取或设置当前处理者的租约令牌。
    /// </summary>
    public string? LeaseToken { get; set; }
    /// <summary>
    /// 获取或设置租约到期的 Unix 毫秒时间戳。
    /// </summary>
    public long LeaseUntil { get; set; }
    /// <summary>
    /// 获取或设置记录版本。
    /// </summary>
    public long Version { get; set; } = 1;
    /// <summary>
    /// 获取或设置连接创建的 Unix 毫秒时间戳。
    /// </summary>
    public long CreatedAt { get; set; }
    /// <summary>
    /// 根据连接作用域信息创建存储作用域。
    /// </summary>
    /// <returns>主机作用域或对应租户作用域。</returns>
    public StorageScope Scope() => ScopeKind == StorageScopeKind.Host ? StorageScope.Host : StorageScope.ForTenant(TenantId!);
}

/// <summary>
/// 已确认网盘文件的持久化映射。
/// </summary>
/// <remarks>仅持久化已校验的远端结果；摘要为实际内容 SHA-256 的 Base64。</remarks>
public sealed class RemoteDriveObject
{
    /// <summary>
    /// 获取或设置由位置编码得到的文件映射标识。
    /// </summary>
    public string Id { get; set; } = "";
    /// <summary>
    /// 获取或设置关联的网盘连接标识。
    /// </summary>
    public Guid ConnectionId { get; set; }
    /// <summary>
    /// 获取或设置关联的传输任务标识。
    /// </summary>
    public Guid TransferId { get; set; }
    /// <summary>
    /// 获取或设置命名文件存储实例。
    /// </summary>
    public string StoreName { get; set; } = "";
    /// <summary>
    /// 获取或设置持久化存储的隔离范围键。
    /// </summary>
    public string ScopeKey { get; set; } = "";
    /// <summary>
    /// 获取或设置逻辑文件容器名称。
    /// </summary>
    public string Container { get; set; } = "";
    /// <summary>
    /// 获取或设置逻辑容器内的文件对象键。
    /// </summary>
    public string ObjectKey { get; set; } = "";
    /// <summary>
    /// 获取或设置远端文件描述的 JSON 文本。
    /// </summary>
    public string RemoteFileJson { get; set; } = "";
    /// <summary>
    /// 获取或设置文件元数据的 JSON 文本。
    /// </summary>
    public string MetadataJson { get; set; } = "{}";
    /// <summary>
    /// 获取或设置内容的 MIME 类型。
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";
    /// <summary>
    /// 获取或设置内容 SHA-256 摘要的 Base64 文本。
    /// </summary>
    public string Sha256 { get; set; } = "";
    /// <summary>
    /// 获取或设置内容字节数。
    /// </summary>
    public long Length { get; set; }
    /// <summary>
    /// 获取或设置文件映射是否已标记删除。
    /// </summary>
    public bool Deleted { get; set; }
    /// <summary>
    /// 获取或设置记录版本。
    /// </summary>
    public long Version { get; set; } = 1;
    /// <summary>
    /// 根据存储、作用域和文件地址生成对象键。
    /// </summary>
    /// <param name="storeName">存储名称。</param>
    /// <param name="scopeKey">作用域标识。</param>
    /// <param name="address">文件地址。</param>
    /// <returns>由这些字段的 UTF-8 文本生成的小写 SHA-256 十六进制键。</returns>
    public static string Key(string storeName, string scopeKey, FileAddress address)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(storeName + "\n" + scopeKey + "\n" + address.Container + "\n" + address.Key))).ToLowerInvariant();
}

/// <summary>
/// 网盘传输的中转与恢复检查点。
/// </summary>
/// <remarks>远端与中转检查点。CheckpointJson 不保存访问令牌、STS 密钥或签名 URL。</remarks>
public sealed class RemoteDriveTransfer
{
    /// <summary>
    /// 获取或设置网盘传输任务标识。
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 获取或设置关联的网盘连接标识。
    /// </summary>
    public Guid ConnectionId { get; set; }
    /// <summary>
    /// 获取或设置命名文件存储实例。
    /// </summary>
    public string StoreName { get; set; } = "";
    /// <summary>
    /// 获取或设置持久化存储的隔离范围键。
    /// </summary>
    public string ScopeKey { get; set; } = "";
    /// <summary>
    /// 获取或设置逻辑文件容器名称。
    /// </summary>
    public string Container { get; set; } = "";
    /// <summary>
    /// 获取或设置逻辑容器内的文件对象键。
    /// </summary>
    public string ObjectKey { get; set; } = "";
    /// <summary>
    /// 获取或设置中转文件使用的命名存储实例。
    /// </summary>
    public string StageStoreName { get; set; } = "";
    /// <summary>
    /// 获取或设置中转文件的逻辑容器。
    /// </summary>
    public string StageContainer { get; set; } = "";
    /// <summary>
    /// 获取或设置中转文件的对象键。
    /// </summary>
    public string StageObjectKey { get; set; } = "";
    /// <summary>
    /// 获取或设置中转分片上传会话标识。
    /// </summary>
    public string? StageUploadId { get; set; }
    /// <summary>
    /// 获取或设置中转文件元数据的 JSON 文本。
    /// </summary>
    public string StageMetadataJson { get; set; } = "{}";
    /// <summary>
    /// 获取或设置文件元数据的 JSON 文本。
    /// </summary>
    public string MetadataJson { get; set; } = "{}";
    /// <summary>
    /// 获取或设置已上传分片清单的 JSON 文本。
    /// </summary>
    public string ManifestJson { get; set; } = "[]";
    /// <summary>
    /// 获取或设置可恢复远端上传进度的 JSON 文本。
    /// </summary>
    public string CheckpointJson { get; set; } = "{}";
    /// <summary>
    /// 获取或设置被替换远端文件的 JSON 描述。
    /// </summary>
    /// <remarks>没有被替换的远端文件时为 null。</remarks>
    public string? PreviousRemoteFileJson { get; set; }
    /// <summary>
    /// 获取或设置提交映射时预期的对象版本。
    /// </summary>
    public long ExpectedObjectVersion { get; set; }
    /// <summary>
    /// 获取或设置内容的 MIME 类型。
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";
    /// <summary>
    /// 获取或设置内容字节数。
    /// </summary>
    public long Length { get; set; }
    /// <summary>
    /// 获取或设置每个分片的字节数。
    /// </summary>
    public int PartSize { get; set; }
    /// <summary>
    /// 获取或设置中转文件是否已完成合并。
    /// </summary>
    public bool StageCompleted { get; set; }
    /// <summary>
    /// 获取或设置中转内容是否已完成清理。
    /// </summary>
    public bool CleanupCompleted { get; set; }
    /// <summary>
    /// 获取或设置清理失败后的下次检查时间。
    /// </summary>
    /// <remarks>0 表示尚未退避。</remarks>
    public long CleanupNextAttemptAt { get; set; }
    /// <summary>
    /// 获取或设置不包含远端正文、签名或凭据的清理失败分类。
    /// </summary>
    public string? CleanupErrorCode { get; set; }
    /// <summary>
    /// 获取或设置网盘传输的当前处理阶段。
    /// </summary>
    public RemoteDriveTransferState State { get; set; }
    /// <summary>
    /// 获取或设置当前处理者的租约令牌。
    /// </summary>
    public string? LeaseToken { get; set; }
    /// <summary>
    /// 获取或设置租约到期的 Unix 毫秒时间戳。
    /// </summary>
    public long LeaseUntil { get; set; }
    /// <summary>
    /// 获取或设置到期的 Unix 毫秒时间戳。
    /// </summary>
    public long ExpiresAt { get; set; }
    /// <summary>
    /// 获取或设置记录版本。
    /// </summary>
    public long Version { get; set; } = 1;
    /// <summary>
    /// 获取或设置传输创建的 Unix 毫秒时间戳。
    /// </summary>
    public long CreatedAt { get; set; }
}

/// <summary>
/// 网盘访问与刷新凭据。
/// </summary>
public sealed class RemoteDriveCredentials
{
    /// <summary>
    /// 获取或设置网盘访问令牌。
    /// </summary>
    public string AccessToken { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘刷新令牌。
    /// </summary>
    public string RefreshToken { get; set; } = "";
    /// <summary>
    /// 获取或设置到期的 Unix 毫秒时间戳。
    /// </summary>
    public long ExpiresAt { get; set; }
}

/// <summary>
/// 网盘授权流程的临时状态。
/// </summary>
public sealed class RemoteDriveAuthorization
{
    /// <summary>
    /// 获取或设置访问目标地址。
    /// </summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// 获取或设置防止授权回调伪造的关联状态。
    /// </summary>
    public string State { get; set; } = "";
    /// <summary>
    /// 获取或设置OAuth PKCE 校验值。
    /// </summary>
    public string Verifier { get; set; } = "";
    /// <summary>
    /// 获取或设置设备授权标识。
    /// </summary>
    public string DeviceId { get; set; } = "";
    /// <summary>
    /// 获取或设置设备授权返回的时间文本。
    /// </summary>
    public string DeviceTime { get; set; } = "";
    /// <summary>
    /// 获取或设置设备授权返回的签名。
    /// </summary>
    public string DeviceSign { get; set; } = "";
    /// <summary>
    /// 获取或设置OAuth 授权回调地址。
    /// </summary>
    public string RedirectUri { get; set; } = "";
    /// <summary>
    /// 获取或设置到期的 Unix 毫秒时间戳。
    /// </summary>
    public long ExpiresAt { get; set; }
}

/// <summary>
/// 已授权网盘账户与磁盘标识。
/// </summary>
public sealed class RemoteDriveAccount
{
    /// <summary>
    /// 获取或设置远端网盘账户标识。
    /// </summary>
    public string AccountId { get; set; } = "";
    /// <summary>
    /// 获取或设置远端网盘磁盘标识。
    /// </summary>
    public string DriveId { get; set; } = "";
}

/// <summary>
/// 网盘文件的远端标识与属性。
/// </summary>
public sealed class RemoteDriveFile
{
    /// <summary>
    /// 获取或设置网盘返回的文件标识。
    /// </summary>
    public string Id { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘返回的父目录标识。
    /// </summary>
    public string ParentId { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘文件名称。
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// 获取或设置115 网盘文件提取标识。
    /// </summary>
    public string? PickCode { get; set; }
    /// <summary>
    /// 获取或设置内容字节数。
    /// </summary>
    public long Length { get; set; }
    /// <summary>
    /// 获取或设置网盘返回的内容校验摘要。
    /// </summary>
    public string? ContentHash { get; set; }
    /// <summary>
    /// 获取或设置网盘返回的文件版本。
    /// </summary>
    public string? Version { get; set; }
    /// <summary>
    /// 获取或设置远端文件的修改时间。
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }
}

/// <summary>
/// 网盘文件下载地址与请求头。
/// </summary>
public sealed class RemoteDriveDownload
{
    /// <summary>
    /// 获取或设置访问目标地址。
    /// </summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// 获取或设置访问文件时必须携带的请求头。
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 可恢复网盘上传的远端进度。
/// </summary>
public sealed class RemoteDriveCheckpoint
{
    /// <summary>
    /// 获取或设置远端上传文件名称。
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// 获取或设置远端上传文件标识。
    /// </summary>
    public string? FileId { get; set; }
    /// <summary>
    /// 获取或设置分片上传会话标识。
    /// </summary>
    public string? UploadId { get; set; }
    /// <summary>
    /// 获取或设置115 网盘文件提取标识。
    /// </summary>
    public string? PickCode { get; set; }
    /// <summary>
    /// 获取或设置远端上传存储桶。
    /// </summary>
    public string? Bucket { get; set; }
    /// <summary>
    /// 获取或设置逻辑容器内的文件对象键。
    /// </summary>
    public string? ObjectKey { get; set; }
    /// <summary>
    /// 获取或设置待执行业务回调。
    /// </summary>
    public string? Callback { get; set; }
    /// <summary>
    /// 获取或设置远端上传回调的变量参数。
    /// </summary>
    public string? CallbackVariables { get; set; }
    /// <summary>
    /// 获取或设置上传内容的 SHA-1 摘要。
    /// </summary>
    public string? Sha1 { get; set; }
    /// <summary>
    /// 获取或设置上传内容前缀的 SHA-1 摘要。
    /// </summary>
    public string? PrefixSha1 { get; set; }
    /// <summary>
    /// 获取或设置内容 SHA-256 摘要的 Base64 文本。
    /// </summary>
    public string? Sha256 { get; set; }
    /// <summary>
    /// 获取或设置每个分片的字节数。
    /// </summary>
    public long PartSize { get; set; } = 16 * 1024 * 1024;
    /// <summary>
    /// 获取或设置按分片序号保存的远端分片标识。
    /// </summary>
    public Dictionary<int, string> Parts { get; set; } = new();
    /// <summary>
    /// 获取或设置已经完成提交的远端文件描述。
    /// </summary>
    public RemoteDriveFile? CompletedFile { get; set; }
}

/// <summary>
/// 网盘分片上传回执。
/// </summary>
public sealed class RemoteDrivePartReceipt
{
    /// <summary>
    /// 获取或设置从 1 开始的分片序号。
    /// </summary>
    public int Number { get; set; }
    /// <summary>
    /// 获取或设置内容字节数。
    /// </summary>
    public long Length { get; set; }
    /// <summary>
    /// 获取或设置后端返回的不透明内容标识。
    /// </summary>
    public string ETag { get; set; } = "";
    /// <summary>
    /// 获取或设置内容 SHA-256 摘要的 Base64 文本。
    /// </summary>
    public string? Sha256 { get; set; }
    /// <summary>
    /// 将远程分片回执转换为文件分片。
    /// </summary>
    /// <returns>包含编号、长度、ETag 和 SHA-256 的文件分片。</returns>
    public FilePart ToPart() => new() { Number = Number, Length = Length, ETag = ETag, Sha256 = Sha256 };
    /// <summary>
    /// 根据文件分片创建远程分片回执。
    /// </summary>
    /// <param name="part">要转换的文件分片。</param>
    /// <returns>包含编号、长度、ETag 和 SHA-256 的远程分片回执。</returns>
    public static RemoteDrivePartReceipt From(FilePart part) => new() { Number = part.Number, Length = part.Length, ETag = part.ETag, Sha256 = part.Sha256 };
}
