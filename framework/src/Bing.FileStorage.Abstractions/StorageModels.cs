using System.Text;
using System.Text.RegularExpressions;

namespace Bing.FileStorage;

/// <summary>
/// 标识文件存储门面绑定的隔离范围。
/// </summary>
/// <remarks>
/// 主机范围须由受信任的应用显式选择。
/// </remarks>
public enum StorageScopeKind
{
    /// <summary>
    /// 将存储门面绑定到主机级范围。
    /// </summary>
    Host,
    /// <summary>
    /// 将存储门面绑定到指定租户范围。
    /// </summary>
    Tenant
}

/// <summary>
/// 文件存储的租户或主机隔离范围。
/// </summary>
/// <remarks>一次存储门面绑定的不可变租户或主机范围，不承担业务授权。</remarks>
public sealed class StorageScope
{
    /// <summary>
    /// 初始化一个 <see cref="StorageScope" /> 类型的实例。
    /// </summary>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="tenantId">租户标识。</param>
    private StorageScope(StorageScopeKind kind, string? tenantId) { Kind = kind; TenantId = tenantId; }
    /// <summary>
    /// 获取文件存储隔离范围的种类。
    /// </summary>
    public StorageScopeKind Kind { get; }
    /// <summary>
    /// 获取租户范围的租户标识。
    /// </summary>
    /// <remarks>主机范围返回 null；租户标识须为 1 至 128 个 UTF-8 字节。</remarks>
    public string? TenantId { get; }
    /// <summary>
    /// 获取显式选择的主机存储范围。
    /// </summary>
    public static StorageScope Host { get; } = new(StorageScopeKind.Host, null);
    /// <summary>
    /// 创建具有租户隔离范围的存储作用域。
    /// </summary>
    /// <param name="tenantId">租户标识，UTF-8 编码长度须为 1 至 128 字节。</param>
    /// <returns>包含租户标识及其键的租户存储作用域。</returns>
    /// <remarks>
    /// 作用域键使用租户标识的 SHA-256 摘要，不包含原始标识。
    /// </remarks>
    public static StorageScope ForTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || new UTF8Encoding(false, true).GetByteCount(tenantId) > 128)
            throw new ArgumentException("租户标识须为 1 至 128 个 UTF-8 字节。", nameof(tenantId));
        return new StorageScope(StorageScopeKind.Tenant, tenantId);
    }
    /// <summary>
    /// 获取跨文件系统保持无歧义的隔离范围段。
    /// </summary>
    /// <remarks>跨大小写不敏感文件系统仍无歧义的范围段。</remarks>
    public string Key => Kind == StorageScopeKind.Host ? "h" :
        "t-" + TenantKey(TenantId!);
    /// <summary>
    /// 计算租户标识对应的稳定 SHA-256 键。
    /// </summary>
    /// <param name="tenantId">租户标识。</param>
    /// <returns>租户标识对应的小写 SHA-256 十六进制键。</returns>
    /// <remarks>
    /// 返回小写十六进制编码的摘要。
    /// </remarks>
    private static string TenantKey(string tenantId)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(tenantId)).Select(b => b.ToString("x2")));
    }
}

/// <summary>
/// 文件存储可移植名称校验工具。
/// </summary>
/// <remarks>
/// 验证所有 Provider 共用的可移植名称。
/// </remarks>
public static class FileStorageNames
{
    /// <summary>
    /// 定义文件存储名称校验使用的标识符正则表达式。
    /// </summary>
    private static readonly Regex Identifier = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);
    /// <summary>
    /// 保存文件存储名称校验保留的设备名称集合。
    /// </summary>
    private static readonly HashSet<string> Reserved = new(new[] { "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" }, StringComparer.Ordinal);
    /// <summary>
    /// 验证并返回符合文件存储规则的标识符。
    /// </summary>
    /// <param name="value">待验证的名称。</param>
    /// <param name="parameterName">校验失败时写入参数异常的参数名。</param>
    /// <returns>通过标识符格式和保留名称校验的原值。</returns>
    /// <remarks>
    /// 只接受小写字母、数字、下划线和连字符，并拒绝设备保留名称。
    /// </remarks>
    public static string ValidateIdentifier(string value, string parameterName)
    {
        if (value == null || !Identifier.IsMatch(value) || Reserved.Contains(value))
            throw new ArgumentException("名称只允许小写字母、数字、下划线或连字符，且不能为设备保留名。", parameterName);
        return value;
    }
    /// <summary>
    /// 验证对象键的长度及各路径段。
    /// </summary>
    /// <param name="value">以斜杠分隔的对象键。</param>
    /// <returns>各路径段均通过标识符校验的原文件键。</returns>
    /// <remarks>
    /// 对象键长度须为 1 至 512 个字符，每个路径段还须符合存储标识符规则。
    /// </remarks>
    public static string ValidateKey(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 512) throw new ArgumentException("对象键长度须为 1 至 512。", nameof(value));
        foreach (var segment in value.Split('/')) ValidateIdentifier(segment, nameof(value));
        return value;
    }
}

/// <summary>
/// 文件在逻辑容器内的对象地址。
/// </summary>
/// <remarks>逻辑容器和对象键；不接受磁盘路径或原始文件名。</remarks>
public sealed class FileAddress
{
    /// <summary>
    /// 初始化一个 <see cref="FileAddress" /> 类型的实例。
    /// </summary>
    /// <param name="container">逻辑文件容器名称。</param>
    /// <param name="key">对象在容器内的键。</param>
    public FileAddress(string container, string key)
    {
        Container = FileStorageNames.ValidateIdentifier(container, nameof(container));
        Key = FileStorageNames.ValidateKey(key);
    }
    /// <summary>
    /// 获取逻辑文件容器名称。
    /// </summary>
    public string Container { get; }
    /// <summary>
    /// 获取逻辑容器内的可移植对象键。
    /// </summary>
    /// <remarks>长度为 1 至 512 个字符，各路径段必须符合可移植名称规则。</remarks>
    public string Key { get; }
}

/// <summary>
/// 可持久化的文件存储位置。
/// </summary>
/// <remarks>必须持久化的文件位置；不能用临时 URL 代替。</remarks>
public sealed class FileLocation
{
    /// <summary>
    /// 初始化一个 <see cref="FileLocation" /> 类型的实例。
    /// </summary>
    /// <param name="storeName">用于标识文件存储后端的名称。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="address">文件在逻辑容器中的地址。</param>
    public FileLocation(string storeName, StorageScope scope, FileAddress address)
    {
        StoreName = FileStorageNames.ValidateIdentifier(storeName, nameof(storeName));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }
    /// <summary>
    /// 获取命名文件存储实例。
    /// </summary>
    public string StoreName { get; }
    /// <summary>
    /// 获取操作隔离范围。
    /// </summary>
    public StorageScope Scope { get; }
    /// <summary>
    /// 获取目标地址。
    /// </summary>
    public FileAddress Address { get; }
}

/// <summary>
/// 标识写入文件时对已存在对象的处理方式。
/// </summary>
public enum FileWriteMode
{
    /// <summary>
    /// 创建或替换目标对象。
    /// </summary>
    Overwrite,
    /// <summary>
    /// 仅在目标对象不存在时创建文件。
    /// </summary>
    /// <remarks>
    /// 后端必须以原子方式保证创建条件。
    /// </remarks>
    CreateOnly
}

/// <summary>
/// 声明文件存储后端支持的可移植能力。
/// </summary>
[Flags]
public enum FileStorageCapabilities
{
    /// <summary>
    /// 不声明可选的文件存储能力。
    /// </summary>
    None = 0,
    /// <summary>
    /// 支持目标不存在时的原子创建。
    /// </summary>
    CreateOnly = 1,
    /// <summary>
    /// 支持将文件拆分为多个分片上传。
    /// </summary>
    Multipart = 2,
    /// <summary>
    /// 支持为文件下载生成签名请求。
    /// </summary>
    SignedDownload = 4,
    /// <summary>
    /// 支持为分片上传生成签名请求。
    /// </summary>
    SignedParts = 8,
    /// <summary>
    /// 支持按字节范围读取文件。
    /// </summary>
    Range = 16,
    /// <summary>
    /// 支持通过服务端上传回执确认分片内容。
    /// </summary>
    /// <remarks>
    /// 远端分片回执可能不含摘要，校验必须依据服务端实际持久化的上传结果。
    /// </remarks>
    ServerVerifiedParts = 32,
    /// <summary>
    /// 完成对象写入后仍需执行分片清理。
    /// </summary>
    /// <remarks>
    /// 完成方法可幂等重试，用于清理本地分片清单。
    /// </remarks>
    RequiresCompletionCleanup = 64
}

/// <summary>
/// 文件写入的目标与内容描述。
/// </summary>
/// <remarks>从输入流当前位置读取 Length 个字节；输入流仍由调用方拥有。</remarks>
public sealed class FileWriteRequest
{
    /// <summary>
    /// 初始化一个 <see cref="FileWriteRequest" /> 类型的实例。
    /// </summary>
    /// <param name="address">文件在逻辑容器中的地址。</param>
    /// <param name="length">内容的长度，单位为字节。</param>
    /// <param name="mode">当前操作采用的模式。</param>
    /// <param name="contentType">内容的媒体类型；未指定时使用默认类型。</param>
    /// <param name="metadata">随对象保存的元数据。</param>
    public FileWriteRequest(FileAddress address, long length, FileWriteMode mode, string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : this(address, length, mode, contentType, metadata, false) { }

    /// <summary>
    /// 初始化一个 <see cref="FileWriteRequest" /> 类型的实例。
    /// </summary>
    /// <param name="address">文件在逻辑容器中的地址。</param>
    /// <param name="length">内容的长度，单位为字节。</param>
    /// <param name="mode">当前操作采用的模式。</param>
    /// <param name="contentType">内容的媒体类型；未指定时使用默认类型。</param>
    /// <param name="metadata">随对象保存的元数据。</param>
    /// <param name="restoringMultipart">指示当前请求是否用于恢复已存在的分片上传。</param>
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
    /// 还原已有的分片文件写入请求。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <param name="length">文件总长度。</param>
    /// <param name="mode">文件写入模式。</param>
    /// <param name="contentType">可选的文件内容类型。</param>
    /// <param name="metadata">文件元数据。</param>
    /// <returns>标记为还原模式的文件写入请求。</returns>
    public static FileWriteRequest RestoreMultipart(FileAddress address, long length, FileWriteMode mode,
        string? contentType, IReadOnlyDictionary<string, string> metadata) =>
        new(address, length, mode, contentType, metadata, true);
    /// <summary>
    /// 检查元数据键是否属于文件存储保留前缀。
    /// </summary>
    /// <param name="key">要检查的元数据键。</param>
    /// <returns>键以 bing-upload- 开头时返回 true，否则返回 false。</returns>
    public static bool IsMultipartMetadata(string key) => key.StartsWith("bing-upload-", StringComparison.Ordinal);
    /// <summary>
    /// 获取文件写入目标的逻辑地址。
    /// </summary>
    public FileAddress Address { get; }
    /// <summary>
    /// 获取内容字节数。
    /// </summary>
    /// <remarks>必须为非负数；从输入流当前位置读取指定字节数。</remarks>
    public long Length { get; }
    /// <summary>
    /// 获取写入策略。
    /// </summary>
    public FileWriteMode Mode { get; }
    /// <summary>
    /// 获取内容的 MIME 类型。
    /// </summary>
    /// <remarks>构造时未指定内容类型则使用 application/octet-stream。</remarks>
    public string ContentType { get; }
    /// <summary>
    /// 获取随文件保存的自定义元数据。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// 后端返回的文件属性。
/// </summary>
/// <remarks>后端确认的对象属性；ETag 是不透明标识。</remarks>
public sealed class FileProperties
{
    /// <summary>
    /// 获取或设置内容字节数。
    /// </summary>
    public long Length { get; set; }
    /// <summary>
    /// 获取或设置文件最后修改的 UTC 时间。
    /// </summary>
    public DateTimeOffset LastModifiedUtc { get; set; }
    /// <summary>
    /// 获取或设置内容的 MIME 类型。
    /// </summary>
    /// <remarks>后端未提供内容类型时可为空。</remarks>
    public string? ContentType { get; set; }
    /// <summary>
    /// 获取或设置后端返回的不透明内容标识。
    /// </summary>
    /// <remarks>该值是后端不透明标识，不能直接视为内容摘要；后端不提供时可为空。</remarks>
    public string? ETag { get; set; }
    /// <summary>
    /// 获取或设置后端对象版本标识。
    /// </summary>
    /// <remarks>后端不支持对象版本时可为空。</remarks>
    public string? VersionId { get; set; }
    /// <summary>
    /// 获取或设置后端返回的文件元数据。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// 文件写入后的位置与属性。
/// </summary>
public sealed class FileWriteResult
{
    /// <summary>
    /// 初始化一个 <see cref="FileWriteResult" /> 类型的实例。
    /// </summary>
    /// <param name="location">当前文件使用的持久化存储位置。</param>
    /// <param name="properties">随文件位置保存的文件属性。</param>
    public FileWriteResult(FileLocation location, FileProperties properties) { Location = location; Properties = properties; }
    /// <summary>
    /// 获取写入后的完整文件位置。
    /// </summary>
    public FileLocation Location { get; }
    /// <summary>
    /// 获取后端确认的文件属性。
    /// </summary>
    public FileProperties Properties { get; }
}

/// <summary>
/// 文件分片上传会话。
/// </summary>
/// <remarks>受信任应用保存的后端会话句柄；不得直接从客户端反序列化后使用。</remarks>
public sealed class MultipartUpload
{
    /// <summary>
    /// 初始化一个 <see cref="MultipartUpload" /> 类型的实例。
    /// </summary>
    /// <param name="uploadId">后端返回的分片上传标识。</param>
    /// <param name="request">当前操作对应的 HTTP 请求。</param>
    /// <param name="partSize">文件分片的字节大小。</param>
    public MultipartUpload(string uploadId, FileWriteRequest request, int partSize)
    {
        if (string.IsNullOrWhiteSpace(uploadId)) throw new ArgumentException("上传标识不能为空。", nameof(uploadId));
        if (partSize < 5 * 1024 * 1024 || partSize > 128 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(partSize));
        if (request.Length > (long)partSize * 10000) throw new ArgumentOutOfRangeException(nameof(request));
        UploadId = uploadId; Request = request; PartSize = partSize;
    }
    /// <summary>
    /// 获取分片上传会话标识。
    /// </summary>
    public string UploadId { get; }
    /// <summary>
    /// 获取上传会话关联的写入请求。
    /// </summary>
    public FileWriteRequest Request { get; }
    /// <summary>
    /// 获取每个分片的字节数。
    /// </summary>
    public int PartSize { get; }
    /// <summary>
    /// 获取预期上传的分片数量。
    /// </summary>
    public int PartCount => Request.Length == 0 ? 0 : (int)((Request.Length - 1) / PartSize + 1);
    /// <summary>
    /// 获取指定分片在上传请求中的预期字节数。
    /// </summary>
    /// <param name="number">从 1 开始的分片序号，不能超过分片总数。</param>
    /// <returns>指定分片应读取的字节数；最后一片按剩余文件长度计算。</returns>
    /// <remarks>
    /// 除最后一片外长度不超过 PartSize；最后一片按剩余文件长度计算。
    /// </remarks>
    public long GetPartLength(int number)
    {
        if (number < 1 || number > PartCount) throw new ArgumentOutOfRangeException(nameof(number));
        return Math.Min(PartSize, Request.Length - (long)(number - 1) * PartSize);
    }
}

/// <summary>
/// 已上传文件分片的回执。
/// </summary>
public sealed class FilePart
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
    /// 获取或设置分片 SHA-256 的 Base64 文本。
    /// </summary>
    /// <remarks>不等同于 ETag。未提供摘要时为 null；摘要与 ETag 不能互换。</remarks>
    public string? Sha256 { get; set; }
}

/// <summary>
/// 带有效期和必需请求头的文件访问签名。
/// </summary>
public sealed class SignedFileRequest
{
    /// <summary>
    /// 获取或设置访问目标地址。
    /// </summary>
    public Uri Url { get; set; } = null!;
    /// <summary>
    /// 获取或设置访问签名地址时使用的 HTTP 方法。
    /// </summary>
    public string Method { get; set; } = "GET";
    /// <summary>
    /// 获取或设置签名访问请求的到期时间。
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>
    /// 获取或设置访问文件时必须携带的请求头。
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
}
