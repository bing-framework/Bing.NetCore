using System.Security.Cryptography;
namespace Bing.FileStorage;

/// <summary>所有 Provider 共用的流和分片校验，不拥有调用方的流。</summary>
public static class FileStorageStreams
{
    public static string ObjectKey(string storageNamespace, StorageScope scope, FileAddress address)
        => FileStorageNames.ValidateIdentifier(storageNamespace, nameof(storageNamespace)) + "/" + scope.Key + "/" + address.Container + "/" + address.Key;

    public static void ValidateSource(Stream source, long length)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!source.CanRead) throw new ArgumentException("输入流不可读取。", nameof(source));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (source.CanSeek && source.Length - source.Position != length)
            throw new FileStorageException(FileStorageError.InvalidLength, "输入流剩余长度与声明不一致。");
    }

    /// <summary>固定缓冲复制，检查短流和多余字节；返回实际 SHA-256 Base64。</summary>
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

    public static void ValidateParts(MultipartUpload upload, IReadOnlyList<FilePart> parts)
    {
        if (parts.Count != upload.PartCount) throw new FileStorageException(FileStorageError.InvalidPart, "分片数量不完整。");
        for (var index = 0; index < parts.Count; index++)
            if (parts[index].Number != index + 1 || parts[index].Length != upload.GetPartLength(index + 1) || string.IsNullOrWhiteSpace(parts[index].ETag))
                throw new FileStorageException(FileStorageError.InvalidPart, "分片序号、长度或标识无效。");
    }

    public static void ValidateSha256(string? value)
    {
        if (value == null) return;
        try { if (Convert.FromBase64String(value).Length == 32) return; }
        catch (FormatException) { }
        throw new ArgumentException("SHA-256 必须为 32 字节摘要的 Base64。", nameof(value));
    }

    public static Task CopyToAsync(this IFileStorage storage, FileAddress address, Stream destination, CancellationToken cancellationToken = default)
        => storage.ReadAsync(address, (source, ct) => source.CopyToAsync(destination, 128 * 1024, ct), cancellationToken);

    public static FileLocation Location(this IFileStorage storage, FileAddress address) => new(storage.Name, storage.Scope, address);
}

/// <summary>限定回调可读范围，释放包装器不关闭底层流。</summary>
public sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private long _remaining;
    private bool _disposed;
    public BoundedReadStream(Stream inner, long length) { _inner = inner; _remaining = length >= 0 ? length : throw new ArgumentOutOfRangeException(nameof(length)); }
    public override bool CanRead => !_disposed && _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BoundedReadStream));
        if (_remaining == 0) return 0;
        var n = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
        _remaining -= n; return n;
    }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BoundedReadStream));
        if (_remaining == 0) return 0;
        var n = await _inner.ReadAsync(buffer, offset, (int)Math.Min(count, _remaining), cancellationToken).ConfigureAwait(false);
        _remaining -= n; return n;
    }
    protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
