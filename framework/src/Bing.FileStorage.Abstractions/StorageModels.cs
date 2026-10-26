using System.Text;
using System.Text.RegularExpressions;

namespace Bing.FileStorage;

/// <summary>存储范围种类；主机范围必须由受信任的应用显式选择。</summary>
public enum StorageScopeKind { Host, Tenant }

/// <summary>一次存储门面绑定的不可变租户或主机范围，不承担业务授权。</summary>
public sealed class StorageScope
{
    private StorageScope(StorageScopeKind kind, string? tenantId) { Kind = kind; TenantId = tenantId; }
    public StorageScopeKind Kind { get; }
    public string? TenantId { get; }
    public static StorageScope Host { get; } = new(StorageScopeKind.Host, null);
    public static StorageScope ForTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || new UTF8Encoding(false, true).GetByteCount(tenantId) > 128)
            throw new ArgumentException("租户标识须为 1 至 128 个 UTF-8 字节。", nameof(tenantId));
        return new StorageScope(StorageScopeKind.Tenant, tenantId);
    }
    /// <summary>跨大小写不敏感文件系统仍无歧义的范围段。</summary>
    public string Key => Kind == StorageScopeKind.Host ? "h" :
        "t-" + TenantKey(TenantId!);
    private static string TenantKey(string tenantId)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(tenantId)).Select(b => b.ToString("x2")));
    }
}

/// <summary>验证所有 Provider 共用的可移植名称。</summary>
public static class FileStorageNames
{
    private static readonly Regex Identifier = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Reserved = new(new[] { "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" }, StringComparer.Ordinal);
    public static string ValidateIdentifier(string value, string parameterName)
    {
        if (value == null || !Identifier.IsMatch(value) || Reserved.Contains(value))
            throw new ArgumentException("名称只允许小写字母、数字、下划线或连字符，且不能为设备保留名。", parameterName);
        return value;
    }
    public static string ValidateKey(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 512) throw new ArgumentException("对象键长度须为 1 至 512。", nameof(value));
        foreach (var segment in value.Split('/')) ValidateIdentifier(segment, nameof(value));
        return value;
    }
}

/// <summary>逻辑容器和对象键；不接受磁盘路径或原始文件名。</summary>
public sealed class FileAddress
{
    public FileAddress(string container, string key)
    {
        Container = FileStorageNames.ValidateIdentifier(container, nameof(container));
        Key = FileStorageNames.ValidateKey(key);
    }
    public string Container { get; }
    public string Key { get; }
}

/// <summary>必须持久化的文件位置；不能用临时 URL 代替。</summary>
public sealed class FileLocation
{
    public FileLocation(string storeName, StorageScope scope, FileAddress address)
    {
        StoreName = FileStorageNames.ValidateIdentifier(storeName, nameof(storeName));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }
    public string StoreName { get; }
    public StorageScope Scope { get; }
    public FileAddress Address { get; }
}

/// <summary>写入策略。CreateOnly 必须具有后端原子语义。</summary>
public enum FileWriteMode { Overwrite, CreateOnly }

[Flags]
public enum FileStorageCapabilities
{
    None = 0, CreateOnly = 1, Multipart = 2, SignedDownload = 4, SignedParts = 8, Range = 16,
    /// <summary>远端分片可能不返回摘要，必须以服务端实际上传的持久回执确认内容。</summary>
    ServerVerifiedParts = 32,
    /// <summary>对象已提交后仍需调用幂等完成方法清理本地分片清单。</summary>
    RequiresCompletionCleanup = 64
}

/// <summary>从输入流当前位置读取 Length 个字节；输入流仍由调用方拥有。</summary>
public sealed class FileWriteRequest
{
    public FileWriteRequest(FileAddress address, long length, FileWriteMode mode, string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : this(address, length, mode, contentType, metadata, false) { }

    private FileWriteRequest(FileAddress address, long length, FileWriteMode mode, string? contentType,
        IReadOnlyDictionary<string, string>? metadata, bool restoringMultipart)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (!Enum.IsDefined(typeof(FileWriteMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Length = length; Mode = mode; ContentType = contentType ?? "application/octet-stream";
        if (ContentType.Length > 256 || ContentType.Any(char.IsControl)) throw new ArgumentException("无效内容类型。", nameof(contentType));
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata != null)
            foreach (var pair in metadata)
            {
                FileStorageNames.ValidateIdentifier(pair.Key, nameof(metadata));
                if (pair.Value == null || pair.Value.Length > 512 || pair.Value.Any(char.IsControl) || pair.Value.Any(c => c > 127))
                    throw new ArgumentException("存储元数据须为不含控制字符的 ASCII 文本。", nameof(metadata));
                copy.Add(pair.Key, pair.Value);
            }
        // Provider 恢复字段使用独立额度，不能挤占调用方的 16 项自定义元数据。
        var providerCount = restoringMultipart ? copy.Keys.Count(IsMultipartMetadata) : 0;
        if (copy.Count - providerCount > 16 || providerCount > 4)
            throw new ArgumentException("元数据数量超过限制。", nameof(metadata));
        Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(copy);
    }
    /// <summary>
    /// 从受信持久化记录恢复分片请求；bing-upload- 前缀最多保留 4 个 Provider 字段。
    /// 不得用于接收客户端元数据。Provider 在新建上传时仍须拒绝这些保留字段。
    /// </summary>
    public static FileWriteRequest RestoreMultipart(FileAddress address, long length, FileWriteMode mode,
        string? contentType, IReadOnlyDictionary<string, string> metadata) =>
        new(address, length, mode, contentType, metadata, true);
    /// <summary>分片协议内部字段前缀；普通写入调用方不能使用。</summary>
    public static bool IsMultipartMetadata(string key) => key.StartsWith("bing-upload-", StringComparison.Ordinal);
    public FileAddress Address { get; }
    public long Length { get; }
    public FileWriteMode Mode { get; }
    public string ContentType { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>后端确认的对象属性；ETag 是不透明标识。</summary>
public sealed class FileProperties
{
    public long Length { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; }
    public string? ContentType { get; set; }
    public string? ETag { get; set; }
    public string? VersionId { get; set; }
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

public sealed class FileWriteResult
{
    public FileWriteResult(FileLocation location, FileProperties properties) { Location = location; Properties = properties; }
    public FileLocation Location { get; }
    public FileProperties Properties { get; }
}

/// <summary>受信任应用保存的后端会话句柄；不得直接从客户端反序列化后使用。</summary>
public sealed class MultipartUpload
{
    public MultipartUpload(string uploadId, FileWriteRequest request, int partSize)
    {
        if (string.IsNullOrWhiteSpace(uploadId)) throw new ArgumentException("上传标识不能为空。", nameof(uploadId));
        if (partSize < 5 * 1024 * 1024 || partSize > 128 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(partSize));
        if (request.Length > (long)partSize * 10000) throw new ArgumentOutOfRangeException(nameof(request));
        UploadId = uploadId; Request = request; PartSize = partSize;
    }
    public string UploadId { get; }
    public FileWriteRequest Request { get; }
    public int PartSize { get; }
    public int PartCount => Request.Length == 0 ? 0 : (int)((Request.Length - 1) / PartSize + 1);
    public long GetPartLength(int number)
    {
        if (number < 1 || number > PartCount) throw new ArgumentOutOfRangeException(nameof(number));
        return Math.Min(PartSize, Request.Length - (long)(number - 1) * PartSize);
    }
}

public sealed class FilePart
{
    public int Number { get; set; }
    public long Length { get; set; }
    public string ETag { get; set; } = "";
    /// <summary>分片 SHA-256 的 Base64 文本；不等同于 ETag。</summary>
    public string? Sha256 { get; set; }
}

public sealed class SignedFileRequest
{
    public Uri Url { get; set; } = null!;
    public string Method { get; set; } = "GET";
    public DateTimeOffset ExpiresAt { get; set; }
    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
}
