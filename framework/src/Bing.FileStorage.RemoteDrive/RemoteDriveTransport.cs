using System.Net;
using System.Buffers;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 向已授权 HTTPS 地址传输远程盘内容的组件。
/// </summary>
/// <remarks>
/// 只向已授权 API 返回的 HTTPS 地址传输内容，不向下载主机转发 OAuth 凭据。
/// </remarks>
public sealed class RemoteDriveTransport : IDisposable
{
    /// <summary>
    /// 保存远程盘传输使用的 HTTP 客户端。
    /// </summary>
    private readonly HttpClient _http;
    /// <summary>
    /// 保存远程盘传输配置。
    /// </summary>
    private readonly RemoteDriveTransportOptions _options;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveTransport" /> 类型的实例。
    /// </summary>
    public RemoteDriveTransport() : this(new RemoteDriveTransportOptions()) { }
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveTransport" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    public RemoteDriveTransport(RemoteDriveTransportOptions options)
        : this(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }, options) { }
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveTransport" /> 类型的实例。
    /// </summary>
    /// <param name="http">用于发送 HTTP 请求的客户端。</param>
    public RemoteDriveTransport(HttpClient http) : this(http, new RemoteDriveTransportOptions()) { }
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveTransport" /> 类型的实例。
    /// </summary>
    /// <param name="http">用于发送 HTTP 请求的客户端。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <remarks>保留调用方 HttpClient 的显式期限；另加响应头及读写无进展保护。</remarks>
    public RemoteDriveTransport(HttpClient http, RemoteDriveTransportOptions options)
    { _http = http ?? throw new ArgumentNullException(nameof(http)); _options = options.Snapshot(); }
    /// <summary>
    /// 验证远程传输 URL 符合 HTTPS 地址约束。
    /// </summary>
    /// <param name="url">待验证的远程传输地址。</param>
    /// <returns>经过安全校验的绝对 HTTPS URI。</returns>
    /// <remarks>
    /// 地址无法解析、不是 HTTPS、包含用户信息、指向环回地址或以 IP 地址为主机时会抛出配置错误。
    /// </remarks>
    public static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.IsLoopback || IPAddress.TryParse(uri.Host, out _))
            throw new FileStorageException(FileStorageError.Configuration, "远端返回了无效传输地址。");
        return uri;
    }
    /// <summary>
    /// 读取远程文件内容并将响应流交给消费回调。
    /// </summary>
    /// <param name="download">包含下载 URL 和请求头的远程下载信息。</param>
    /// <param name="consume">接收响应流及取消令牌并读取内容的异步回调。</param>
    /// <param name="ct">用于取消下载的令牌。</param>
    public Task ReadAsync(RemoteDriveDownload download, Func<Stream, CancellationToken, Task> consume, CancellationToken ct)
        => ReadAsync(download, consume, ct, _options);

    /// <summary>
    /// 读取远程文件内容并将响应流交给消费回调。
    /// </summary>
    /// <param name="download">包含下载 URL 和请求头的远程下载信息。</param>
    /// <param name="consume">接收响应流及取消令牌并读取内容的异步回调。</param>
    /// <param name="ct">用于取消下载的令牌。</param>
    /// <param name="options">本次读取使用的传输超时选项。</param>
    /// <remarks>
    /// 下载请求只允许传递 User-Agent 请求头；响应流读取期间按传输进度刷新空闲超时。
    /// </remarks>
    internal async Task ReadAsync(RemoteDriveDownload download, Func<Stream, CancellationToken, Task> consume, CancellationToken ct, RemoteDriveTransportOptions options)
    {
        using var watch = new TransferWatch(ct, options);
        using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl(download.Url));
        foreach (var header in download.Headers)
        {
            if (!header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                throw new FileStorageException(FileStorageError.Configuration, "下载请求包含不允许转发的请求头。");
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        try
        {
            using var response = await Send(request, watch.Token);
            Check(response);
            watch.Progress();
            await using var stream = await response.Content.ReadAsStreamAsync(watch.Token);
            using var guarded = new ProgressReadStream(stream, watch);
            await consume(guarded, watch.Token);
            watch.Token.ThrowIfCancellationRequested();
        }
        // 只转换本组件期限触发的取消；消费方有自己的令牌，其取消应原样传播。
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && watch.Token.IsCancellationRequested) { throw TimeoutError(); }
    }
    /// <summary>
    /// 向远程 URL 写入指定长度的流内容。
    /// </summary>
    /// <param name="url">远程写入地址。</param>
    /// <param name="source">要发送的源数据流。</param>
    /// <param name="length">要发送的字节数。</param>
    /// <param name="ct">用于取消上传的令牌。</param>
    /// <returns>远程响应返回的 ETag；响应未提供 ETag 时为 uploaded。</returns>
    /// <remarks>
    /// 长度不能为负数；源数据流必须能提供指定长度的数据，否则写入失败。
    /// </remarks>
    public async Task<string> PutAsync(string url, Stream source, long length, CancellationToken ct)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        using var watch = new TransferWatch(ct, _options);
        using var request = new HttpRequestMessage(HttpMethod.Put, ValidateUrl(url));
        // 只在实际写入传输流后刷新进展；只读取源流不能证明远端仍在接收。
        request.Content = new ProgressContent(source, length, watch);
        try
        {
            using var response = await Send(request, watch.Token);
            Check(response);
            return response.Headers.ETag?.Tag ?? "uploaded";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw TimeoutError(); }
    }
    /// <summary>
    /// 发送远程 HTTP 请求并映射网络请求异常。
    /// </summary>
    /// <param name="request">要发送的 HTTP 请求。</param>
    /// <param name="ct">用于取消 HTTP 请求的令牌。</param>
    /// <returns>包含远端状态码、响应头和内容的 HTTP 响应对象。</returns>
    /// <remarks>
    /// HTTP 请求异常会映射为结果未知的文件存储异常。
    /// </remarks>
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        // HttpClient 自身 Timeout 不会取消调用方令牌，在请求边界单独分类。
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw TimeoutError(); }
        catch (HttpRequestException)
        { throw new FileStorageException(FileStorageError.OutcomeUnknown, "远端传输响应未确认，请重新查询后重试。"); }
    }
    /// <summary>
    /// 创建表示远程传输等待超时的文件存储异常。
    /// </summary>
    /// <returns>表示远端传输超时且操作结果无法确认的文件存储异常。</returns>
    private static FileStorageException TimeoutError() => new(FileStorageError.OutcomeUnknown, "远端传输等待超时，保留检查点后重试。");
    /// <summary>
    /// 检查远程 HTTP 响应并映射失败状态。
    /// </summary>
    /// <param name="response">待检查的 HTTP 响应。</param>
    /// <remarks>
    /// 成功状态码不抛出异常；401 和 403 映射为访问拒绝，404 映射为未找到，其他失败状态映射为服务不可用。
    /// </remarks>
    public static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var error = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? FileStorageError.AccessDenied
            : response.StatusCode == HttpStatusCode.NotFound ? FileStorageError.NotFound
            : FileStorageError.Unavailable;
        // 不把可能包含签名或令牌的响应正文/地址写入异常。
        throw new FileStorageException(error, "网盘传输失败，HTTP " + (int)response.StatusCode);
    }
    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// 关联一次远程传输的取消、响应头和进度通知。
    /// </summary>
    private sealed class TransferWatch : IDisposable
    {
        /// <summary>
        /// 保存传输监视使用的停止取消源。
        /// </summary>
        private readonly CancellationTokenSource _stop;
        /// <summary>
        /// 保存传输监视使用的配置。
        /// </summary>
        private readonly RemoteDriveTransportOptions _options;
        /// <summary>
        /// 初始化一个 <see cref="TransferWatch" /> 类型的实例。
        /// </summary>
        /// <param name="ct">用于取消操作的令牌。</param>
        /// <param name="options">用于初始化实例的配置选项。</param>
        public TransferWatch(CancellationToken ct, RemoteDriveTransportOptions options)
        { _stop = CancellationTokenSource.CreateLinkedTokenSource(ct); _options = options; Headers(); }
        /// <summary>
        /// 获取当前操作的取消令牌。
        /// </summary>
        public CancellationToken Token => _stop.Token;
        /// <summary>
        /// 检查传输取消状态并重置响应头等待超时。
        /// </summary>
        /// <remarks>
        /// 超时期限取自传输选项中的 ResponseHeadersTimeout。
        /// </remarks>
        public void Headers() { Token.ThrowIfCancellationRequested(); _stop.CancelAfter(_options.ResponseHeadersTimeout); }
        /// <summary>
        /// 检查传输取消状态并重置空闲超时。
        /// </summary>
        /// <remarks>
        /// 超时期限取自传输选项中的 IdleTimeout。
        /// </remarks>
        public void Progress() { Token.ThrowIfCancellationRequested(); _stop.CancelAfter(_options.IdleTimeout); }
        /// <inheritdoc />
        public void Dispose() => _stop.Dispose();
    }

    /// <summary>
    /// 复制 HTTP 内容时同步报告远程传输进度。
    /// </summary>
    private sealed class ProgressContent : HttpContent
    {
        /// <summary>
        /// 保存进度内容包装的源流。
        /// </summary>
        private readonly Stream _source;
        /// <summary>
        /// 保存进度内容声明的总长度。
        /// </summary>
        private readonly long _length;
        /// <summary>
        /// 保存进度内容使用的传输监视器。
        /// </summary>
        private readonly TransferWatch _watch;
        /// <summary>
        /// 初始化一个 <see cref="ProgressContent" /> 类型的实例。
        /// </summary>
        /// <param name="source">当前操作读取内容或状态的数据源。</param>
        /// <param name="length">内容的长度，单位为字节。</param>
        /// <param name="watch">用于观察传输取消和进度的监视器。</param>
        public ProgressContent(Stream source, long length, TransferWatch watch)
        { _source = source; _length = length; _watch = watch; Headers.ContentLength = length; }
        /// <inheritdoc />
        protected override bool TryComputeLength(out long length) { length = _length; return true; }
        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => CopyAsync(stream, _watch.Token);
        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct) => CopyAsync(stream, ct);
        /// <summary>
        /// 将源流中声明长度的数据复制到输出流。
        /// </summary>
        /// <param name="output">接收数据的输出流。</param>
        /// <param name="ct">用于取消流复制的令牌。</param>
        /// <remarks>
        /// 源流在声明长度之前结束时会抛出长度错误；复制完成后会开始响应头等待超时。
        /// </remarks>
        private async Task CopyAsync(Stream output, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _watch.Token);
            _watch.Progress();
            var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            try
            {
                var left = _length;
                while (left > 0)
                {
                    var read = await _source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, left)), linked.Token).ConfigureAwait(false);
                    if (read == 0) throw new FileStorageException(FileStorageError.InvalidLength, "上传内容少于声明长度。");
                    await output.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
                    left -= read; _watch.Progress();
                }
                _watch.Headers();
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        // HttpContent 释放不关闭调用方拥有的源流。
    }

    /// <summary>
    /// 读取 HTTP 内容流时同步报告远程传输进度。
    /// </summary>
    private sealed class ProgressReadStream : Stream
    {
        /// <summary>
        /// 保存进度读取流包装的内部流。
        /// </summary>
        private readonly Stream _inner;
        /// <summary>
        /// 保存进度读取流使用的传输监视器。
        /// </summary>
        private readonly TransferWatch _watch;
        /// <summary>
        /// 初始化一个 <see cref="ProgressReadStream" /> 类型的实例。
        /// </summary>
        /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
        /// <param name="watch">用于观察传输取消和进度的监视器。</param>
        public ProgressReadStream(Stream inner, TransferWatch watch) { _inner = inner; _watch = watch; }
        /// <inheritdoc />
        public override bool CanRead => _inner.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, _watch.Token).GetAwaiter().GetResult();
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (ct == _watch.Token)
                return await ReadCoreAsync(buffer, ct).ConfigureAwait(false);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _watch.Token);
            return await ReadCoreAsync(buffer, linked.Token).ConfigureAwait(false);
        }
        /// <summary>
        /// 从远程响应流读取数据并更新传输进度。
        /// </summary>
        /// <param name="buffer">接收读取数据的缓冲区。</param>
        /// <param name="ct">用于取消读取的令牌。</param>
        /// <returns>实际读取的字节数；到达流末尾时为 0。</returns>
        /// <remarks>
        /// 读取到一个或多个字节后会重置空闲超时。
        /// </remarks>
        private async ValueTask<int> ReadCoreAsync(Memory<byte> buffer, CancellationToken ct)
        {
            var read = await _inner.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read > 0) _watch.Progress();
            return read;
        }
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // 底层响应流由 ReadAsync 外层拥有并释放。
    }
}
