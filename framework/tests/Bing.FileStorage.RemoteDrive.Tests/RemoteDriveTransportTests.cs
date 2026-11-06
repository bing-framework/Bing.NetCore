using System.Net;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.FileStorage.RemoteDrive.Tests;

/// <summary>
/// 验证远程盘实际 HTTP 传输边界的测试。
/// </summary>
/// <remarks>
/// 在实际 HttpClient 边界模拟停滞；直接消费生产 HttpContent 和响应流。
/// </remarks>
public sealed class RemoteDriveTransportTests
{
    /// <summary>
    /// 定义远程盘传输测试使用的请求地址。
    /// </summary>
    private const string Url = "https://transport.test/content";
    /// <summary>
    /// 创建短超时的远程网盘传输选项。
    /// </summary>
    /// <returns>用于超时测试的短响应头及空闲超时选项。</returns>
    private static RemoteDriveTransportOptions Short() => new() { ResponseHeadersTimeout = TimeSpan.FromMilliseconds(150), IdleTimeout = TimeSpan.FromMilliseconds(300) };
    /// <summary>
    /// 创建指向测试地址的下载描述。
    /// </summary>
    /// <returns>指向固定测试地址的下载描述。</returns>
    private static RemoteDriveDownload Download() => new() { Url = Url };
    /// <summary>
    /// 创建禁用整体超时的 HTTP 测试客户端。
    /// </summary>
    /// <param name="action">在测试上下文或事务内执行的操作。</param>
    /// <returns>使用指定发送回调且禁用整体超时的客户端。</returns>
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
        => new(new Handler(action)) { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// 验证等待响应头超时且不改写注入客户端的超时设置。
    /// </summary>
    [Fact]
    public async Task MissingResponseHeaders_TimesOutAndPreservesInjectedClientTimeout()
    {
        using var http = Client(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new InvalidOperationException(); });
        http.Timeout = TimeSpan.FromMilliseconds(80);
        using var transport = new RemoteDriveTransport(http, new() { ResponseHeadersTimeout = TimeSpan.FromSeconds(5) });
        Assert.Equal(TimeSpan.FromMilliseconds(80), http.Timeout);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => transport.ReadAsync(Download(), (_, _) => Task.CompletedTask, deadline.Token));
        Assert.Equal(FileStorageError.OutcomeUnknown, error.Error); Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>
    /// 验证响应头截止时间同时适用于下载和上传。
    /// </summary>
    /// <param name="upload">是否测试上传路径；否则测试下载路径。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HeadersDeadline_AppliesToDownloadAndUpload(bool upload)
    {
        using var http = Client(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new InvalidOperationException(); });
        using var transport = new RemoteDriveTransport(http, Short());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var source = new MemoryStream(new byte[] { 1 });
        var error = await Assert.ThrowsAsync<FileStorageException>(() => upload
            ? transport.PutAsync(Url, source, source.Length, deadline.Token)
            : transport.ReadAsync(Download(), (_, _) => Task.CompletedTask, deadline.Token));
        Assert.Equal(FileStorageError.OutcomeUnknown, error.Error); Assert.False(deadline.IsCancellationRequested);
        Assert.True(source.CanRead);
    }

    /// <summary>
    /// 验证下载正文停滞超时后释放响应且不依赖消费方令牌。
    /// </summary>
    [Fact]
    public async Task DownloadBodyStops_TimeoutDisposesResponseAndDoesNotRequireConsumerToken()
    {
        var content = new TimedStream(1, stall: true);
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(content) }));
        using var transport = new RemoteDriveTransport(http, Short());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => transport.ReadAsync(Download(),
            async (stream, _) => { await stream.ReadAsync(new byte[1]); await stream.ReadAsync(new byte[1]); }, deadline.Token));
        Assert.Equal(FileStorageError.OutcomeUnknown, error.Error); Assert.True(content.Disposed);
        Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>
    /// 验证上传读取或写入停滞超时且不关闭调用方源流。
    /// </summary>
    /// <param name="stopWriting">是否停止继续写入测试流。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UploadStopsReadingOrWriting_TimesOutWithoutClosingSource(bool stopWriting)
    {
        using var source = stopWriting ? new TimedStream(1) : new TimedStream(0, stall: true);
        using var http = Client(async (request, ct) =>
        {
            using var output = new TimedStream(0, stallWrites: stopWriting);
            await request.Content!.CopyToAsync(output, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var transport = new RemoteDriveTransport(http, Short());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => transport.PutAsync(Url, source, 1, deadline.Token));
        Assert.Equal(FileStorageError.OutcomeUnknown, error.Error); Assert.False(source.Disposed);
        Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>
    /// 验证持续字节进度允许传输超过响应头及空闲时长。
    /// </summary>
    /// <param name="upload">是否测试上传路径；否则测试下载路径。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ContinuedByteProgress_CanExceedHeadersAndIdleTotalDuration(bool upload)
    {
        var source = new TimedStream(8, delay: 80);
        using var http = Client(async (request, ct) =>
        {
            if (!upload) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) };
            using var output = new MemoryStream();
            await request.Content!.CopyToAsync(output, ct); Assert.Equal(8, output.Length);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var transport = new RemoteDriveTransport(http, Short());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (upload) { await transport.PutAsync(Url, source, 8, deadline.Token); Assert.False(source.Disposed); source.Dispose(); }
        else
        {
            using var output = new MemoryStream();
            await transport.ReadAsync(Download(), (stream, ct) => stream.CopyToAsync(output, ct), deadline.Token);
            Assert.Equal(8, output.Length); Assert.True(source.Disposed);
        }
        Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>
    /// 验证外部取消不会被转换为传输超时。
    /// </summary>
    /// <param name="upload">是否测试上传路径；否则测试下载路径。</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExternalCancellation_IsNotConvertedToTimeout(bool upload)
    {
        using var http = Client(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new InvalidOperationException(); });
        using var transport = new RemoteDriveTransport(http, Short());
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
        using var source = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload
            ? transport.PutAsync(Url, source, 0, stop.Token)
            : transport.ReadAsync(Download(), (_, _) => Task.CompletedTask, stop.Token));
        Assert.True(source.CanRead);
    }

    /// <summary>
    /// 验证空上传和过短输入保留长度契约与源流所有权。
    /// </summary>
    /// <param name="expectedLength">预期的内容字节数。</param>
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task UploadEmptyAndShortInput_PreserveLengthAndOwnership(int expectedLength)
    {
        using var http = Client(async (request, ct) =>
        {
            Assert.Equal(expectedLength, request.Content!.Headers.ContentLength);
            await request.Content.CopyToAsync(Stream.Null, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var transport = new RemoteDriveTransport(http, Short());
        using var source = new MemoryStream();
        if (expectedLength == 0) await transport.PutAsync(Url, source, 0, default);
        else Assert.Equal(FileStorageError.InvalidLength, (await Assert.ThrowsAsync<FileStorageException>(() => transport.PutAsync(Url, source, 1, default))).Error);
        Assert.True(source.CanRead);
    }

    /// <summary>
    /// 验证传输选项先校验并冻结为独立快照。
    /// </summary>
    [Fact]
    public async Task OptionsAreValidatedAndSnapshotted()
    {
        using var http = Client(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new InvalidOperationException(); });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteDriveTransport(http, new() { IdleTimeout = TimeSpan.Zero }));
        var options = Short(); using var transport = new RemoteDriveTransport(http, options);
        options.ResponseHeadersTimeout = TimeSpan.FromDays(1);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        Assert.Equal(FileStorageError.OutcomeUnknown, (await Assert.ThrowsAsync<FileStorageException>(() =>
            transport.ReadAsync(Download(), (_, _) => Task.CompletedTask, deadline.Token))).Error);
        Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>
    /// 为RemoteDriveTransportTests测试提供 HTTP 请求处理替身。
    /// </summary>
    private sealed class Handler : HttpMessageHandler
    {
        /// <summary>
        /// 保存测试处理器处理请求的委托。
        /// </summary>
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _action;
        /// <summary>
        /// 初始化一个 <see cref="Handler" /> 类型的实例。
        /// </summary>
        /// <param name="action">用于处理测试 HTTP 请求并返回响应的委托。</param>
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) => _action = action;
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => _action(request, ct);
    }

    /// <summary>
    /// 包装Timed流并提供对应的读取行为。
    /// </summary>
    private sealed class TimedStream : Stream
    {
        /// <summary>
        /// 记录限时流剩余可读取的字节数。
        /// </summary>
        private int _remaining;
        /// <summary>
        /// 指示限时流是否暂停读取和写入。
        /// </summary>
        private readonly bool _stall, _stallWrites;
        /// <summary>
        /// 保存限时流每次操作的延迟毫秒数。
        /// </summary>
        private readonly int _delay;
        /// <summary>
        /// 初始化一个 <see cref="TimedStream" /> 类型的实例。
        /// </summary>
        /// <param name="bytes">当前内容的字节数或字节数据。</param>
        /// <param name="stall">指示测试传输是否暂停读取，直到收到继续信号。</param>
        /// <param name="stallWrites">指示测试传输是否暂停写入，直到收到继续信号。</param>
        /// <param name="delay">当前操作等待的延迟时长。</param>
        public TimedStream(int bytes, bool stall = false, bool stallWrites = false, int delay = 0)
        { _remaining = bytes; _stall = stall; _stallWrites = stallWrites; _delay = delay; }
        /// <summary>
        /// 获取当前对象是否已经释放。
        /// </summary>
        public bool Disposed { get; private set; }
        /// <inheritdoc />
        public override bool CanRead => !Disposed;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => true;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_remaining == 0) { if (_stall) await Task.Delay(Timeout.InfiniteTimeSpan, ct); return 0; }
            if (_delay > 0) await Task.Delay(_delay, ct);
            buffer.Span[0] = 1; _remaining--; return 1;
        }
        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        { if (_stallWrites) await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
        /// <inheritdoc />
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
