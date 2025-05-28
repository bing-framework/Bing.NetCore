namespace Bing.Content;

/// <summary>
/// 远程流内容
/// </summary>
public class RemoteStreamContent : IRemoteStreamContent
{
    /// <summary>
    /// 流
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// 是否释放流
    /// </summary>
    private readonly bool _disposeStream;

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个<see cref="RemoteStreamContent"/>类型的实例
    /// </summary>
    /// <param name="stream">流</param>
    /// <param name="fileName">文件名（可选）。</param>
    /// <param name="contentType">内容的MIME类型（可选）。</param>
    /// <param name="readOnlyLength">内容的长度（可选）。如果未提供，则尝试从流中获取。</param>
    /// <param name="disposeStream">指示释放 <see cref="RemoteStreamContent"/> 时是否应释放流。</param>
    public RemoteStreamContent(Stream stream, string fileName = null, string contentType = null, long? readOnlyLength = null, bool disposeStream = true)
    {
        _stream = stream;
        FileName = fileName;
        if (contentType != null)
            ContentType = contentType;
        ContentLength = readOnlyLength ?? (_stream.CanSeek ? _stream.Length - stream.Position : null);
        _disposeStream = disposeStream;
    }

    /// <inheritdoc />
    public virtual string FileName { get; }

    /// <inheritdoc />
    public virtual string ContentType { get; } = "application/octet-stream";

    /// <inheritdoc />
    public virtual long? ContentLength { get; }

    /// <inheritdoc />
    public virtual Stream GetStream() => _stream;

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_disposed || !_disposeStream)
            return;

        _disposed = true;
        _stream?.Dispose();
    }
}
