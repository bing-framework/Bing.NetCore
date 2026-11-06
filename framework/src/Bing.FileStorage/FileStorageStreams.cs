using System.Security.Cryptography;
namespace Bing.FileStorage;

/// <summary>
/// 文件存储 Provider 共用的流与分片校验工具。
/// </summary>
/// <remarks>
/// 所有 Provider 共用的流和分片校验，不拥有调用方的流。
/// </remarks>
public static class FileStorageStreams
{
    /// <summary>
    /// 为文件地址生成包含命名空间和作用域的对象键。
    /// </summary>
    /// <param name="storageNamespace">文件存储命名空间。</param>
    /// <param name="scope">限定对象所属租户或作用域的键。</param>
    /// <param name="address">包含容器和对象键的文件地址。</param>
    /// <returns>由命名空间、作用域键、容器和文件键组成的存储对象键。</returns>
    /// <remarks>
    /// 返回按“命名空间/作用域/容器/对象键”组合的稳定路径。
    /// </remarks>
    public static string ObjectKey(string storageNamespace, StorageScope scope, FileAddress address)
        => FileStorageNames.ValidateIdentifier(storageNamespace, nameof(storageNamespace)) + "/" + scope.Key + "/" + address.Container + "/" + address.Key;

    /// <summary>
    /// 验证文件写入源及声明的剩余字节数。
    /// </summary>
    /// <param name="source">待读取的输入流。</param>
    /// <param name="length">从当前位置起的预期字节数。</param>
    /// <remarks>
    /// 流必须可读且长度不能为负数；可定位流的实际剩余长度必须等于声明值。
    /// </remarks>
    public static void ValidateSource(Stream source, long length)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!source.CanRead) throw new ArgumentException("输入流不可读取。", nameof(source));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (source.CanSeek && source.Length - source.Position != length)
            throw new FileStorageException(FileStorageError.InvalidLength, "输入流剩余长度与声明不一致。");
    }

    /// <summary>
    /// 复制指定长度的流并计算内容 SHA-256。
    /// </summary>
    /// <param name="source">提供待复制内容的输入流。</param>
    /// <param name="destination">接收复制内容的输出流。</param>
    /// <param name="length">必须复制的字节数。</param>
    /// <param name="cancellationToken">用于取消读取和写入的令牌。</param>
    /// <returns>复制内容的 Base64 编码 SHA-256。</returns>
    /// <remarks>
    /// 源流未提供恰好 length 个字节时抛出长度错误。
    /// </remarks>
    public static async Task<string> CopyExactAsync(Stream source, Stream destination, long length, CancellationToken cancellationToken)
    {
        ValidateSource(source, length);
        var buffer = new byte[128 * 1024];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long remaining = length;
        while (remaining > 0)
        {
            var read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new FileStorageException(FileStorageError.InvalidLength, "输入流短于声明长度。");
            await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
        if (await source.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false) != 0)
            throw new FileStorageException(FileStorageError.InvalidLength, "输入流超过声明长度。");
        return Convert.ToBase64String(hash.GetHashAndReset());
    }

    /// <summary>
    /// 验证分片集合是否完整且按序匹配上传计划。
    /// </summary>
    /// <param name="upload">包含分片数量及长度计划的上传描述。</param>
    /// <param name="parts">待验证的分片结果列表。</param>
    /// <remarks>
    /// 分片数、序号、每片长度或 ETag 无效时抛出 InvalidPart 文件存储异常。
    /// </remarks>
    public static void ValidateParts(MultipartUpload upload, IReadOnlyList<FilePart> parts)
    {
        if (parts.Count != upload.PartCount) throw new FileStorageException(FileStorageError.InvalidPart, "分片数量不完整。");
        for (var index = 0; index < parts.Count; index++)
            if (parts[index].Number != index + 1 || parts[index].Length != upload.GetPartLength(index + 1) || string.IsNullOrWhiteSpace(parts[index].ETag))
                throw new FileStorageException(FileStorageError.InvalidPart, "分片序号、长度或标识无效。");
    }

    /// <summary>
    /// 验证 SHA-256 摘要的 Base64 编码格式。
    /// </summary>
    /// <param name="value">可选的 Base64 摘要文本；null 表示未提供摘要。</param>
    /// <remarks>
    /// 非 null 值必须解码为 32 字节，否则抛出 ArgumentException。
    /// </remarks>
    public static void ValidateSha256(string? value)
    {
        if (value == null) return;
        try { if (Convert.FromBase64String(value).Length == 32) return; }
        catch (FormatException) { }
        throw new ArgumentException("SHA-256 必须为 32 字节摘要的 Base64。", nameof(value));
    }

    /// <summary>
    /// 异步将指定文件复制到目标流。
    /// </summary>
    /// <param name="storage">读取源文件的存储服务。</param>
    /// <param name="address">要读取的文件地址。</param>
    /// <param name="destination">接收文件内容的目标流。</param>
    /// <param name="cancellationToken">用于取消读取及复制的令牌。</param>
    /// <remarks>
    /// 通过存储读取回调复制内容，缓冲区大小为 128 KiB。
    /// </remarks>
    public static Task CopyToAsync(this IFileStorage storage, FileAddress address, Stream destination, CancellationToken cancellationToken = default)
        => storage.ReadAsync(address, (source, ct) => source.CopyToAsync(destination, 128 * 1024, ct), cancellationToken);

    /// <summary>
    /// 根据存储实例与文件地址创建文件位置描述。
    /// </summary>
    /// <param name="storage">提供存储名称和作用域的实例。</param>
    /// <param name="address">文件地址。</param>
    /// <returns>包含存储名称、作用域和文件地址的文件位置。</returns>
    /// <remarks>
    /// 结果包含存储名称、作用域和文件地址。
    /// </remarks>
    public static FileLocation Location(this IFileStorage storage, FileAddress address) => new(storage.Name, storage.Scope, address);
}

/// <summary>
/// 限定可读取字节范围的流包装器。
/// </summary>
/// <remarks>
/// 限定回调可读范围，释放包装器不关闭底层流。
/// </remarks>
public sealed class BoundedReadStream : Stream
{
    /// <summary>
    /// 保存有界读取流包装的内部流。
    /// </summary>
    private readonly Stream _inner;
    /// <summary>
    /// 记录有界读取流剩余可读取的字节数。
    /// </summary>
    private long _remaining;
    /// <summary>
    /// 指示有界读取流是否已释放。
    /// </summary>
    private bool _disposed;
    /// <summary>
    /// 初始化一个 <see cref="BoundedReadStream" /> 类型的实例。
    /// </summary>
    /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
    /// <param name="length">内容的长度，单位为字节。</param>
    public BoundedReadStream(Stream inner, long length) { _inner = inner; _remaining = length >= 0 ? length : throw new ArgumentOutOfRangeException(nameof(length)); }
    /// <inheritdoc />
    public override bool CanRead => !_disposed && _inner.CanRead;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BoundedReadStream));
        if (_remaining == 0) return 0;
        var n = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
        _remaining -= n; return n;
    }
    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BoundedReadStream));
        if (_remaining == 0) return 0;
        var n = await _inner.ReadAsync(buffer, offset, (int)Math.Min(count, _remaining), cancellationToken).ConfigureAwait(false);
        _remaining -= n; return n;
    }
    /// <inheritdoc />
    protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }
    /// <inheritdoc />
    public override void Flush() { }
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
