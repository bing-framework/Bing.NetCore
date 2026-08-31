namespace Bing.Content;

/// <summary>
/// 使用现有流作为远程内容的默认实现。
/// </summary>
public class RemoteStreamContent : IRemoteStreamContent
{
    /// <summary>
    /// 保存作为内容返回的源流。
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// 指示释放当前内容时是否同时释放源流。
    /// </summary>
    private readonly bool _disposeStream;

    /// <summary>
    /// 指示当前内容是否已执行流释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 使用内容流和可选元数据初始化 <see cref="RemoteStreamContent"/> 的实例。
    /// </summary>
    /// <param name="stream">作为内容返回的源流。</param>
    /// <param name="fileName">可选的内容文件名。</param>
    /// <param name="contentType">可选的 MIME 类型；为空时使用默认 MIME 类型。</param>
    /// <param name="readOnlyLength">可选的内容长度；未提供时仅为可定位流计算当前剩余长度。</param>
    /// <param name="disposeStream">释放当前对象时是否同时释放 <paramref name="stream"/>，默认值为 <c>true</c>。</param>
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
    /// <remarks>原样返回构造时提供的流，不调整其当前位置。</remarks>
    public virtual Stream GetStream() => _stream;

    /// <inheritdoc />
    /// <remarks>仅在构造时启用流释放且尚未释放时处置源流。</remarks>
    public virtual void Dispose()
    {
        if (_disposed || !_disposeStream)
            return;

        _disposed = true;
        _stream?.Dispose();
    }
}
