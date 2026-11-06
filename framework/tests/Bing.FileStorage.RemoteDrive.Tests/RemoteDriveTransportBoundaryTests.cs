using System.Net;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.FileStorage.RemoteDrive.Tests;

/// <summary>验证调用方取消、HTTP 错误及凭据转发边界，不依赖云账号。</summary>
public sealed class RemoteDriveTransportBoundaryTests
{
    private const string Url = "https://transport.test/file?secret=do-not-log";

    /// <summary>消费回调自己的取消必须原样传播，不能伪装为网络超时而触发重试。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsumerCancellation_IsNotNetworkTimeout(bool cancelledToken)
    {
        using var body = new MemoryStream(new byte[] { 1 });
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) })));
        using var transport = new RemoteDriveTransport(http);
        using var stop = new CancellationTokenSource();
        if (cancelledToken) stop.Cancel();
        var expected = new OperationCanceledException(stop.Token);
        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ReadAsync(
            new() { Url = Url }, (_, _) => Task.FromException(expected), CancellationToken.None));
        Assert.Same(expected, actual);
        Assert.False(body.CanRead);
    }

    /// <summary>HTTP 错误必须稳定分类，且错误正文和签名参数不能进入异常。</summary>
    [Theory]
    [InlineData(401, FileStorageError.AccessDenied)]
    [InlineData(403, FileStorageError.AccessDenied)]
    [InlineData(404, FileStorageError.NotFound)]
    [InlineData(429, FileStorageError.Unavailable)]
    [InlineData(500, FileStorageError.Unavailable)]
    [InlineData(503, FileStorageError.Unavailable)]
    public async Task DownloadHttpErrors_AreClassifiedWithoutSecrets(int status, FileStorageError expected)
    {
        using var body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("private-token"));
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)status) { Content = new StreamContent(body) })));
        using var transport = new RemoteDriveTransport(http);
        var called = false;
        var error = await Assert.ThrowsAsync<FileStorageException>(() => transport.ReadAsync(new() { Url = Url },
            (_, _) => { called = true; return Task.CompletedTask; }, default));
        Assert.Equal(expected, error.Error);
        Assert.False(called);
        Assert.False(body.CanRead);
        Assert.DoesNotContain("private-token", error.ToString());
        Assert.DoesNotContain("do-not-log", error.ToString());
    }

    /// <summary>禁止将授权或 Cookie 发送给内容域名，拒绝发生在网络调用之前。</summary>
    [Theory]
    [InlineData("Authorization")]
    [InlineData("Cookie")]
    public async Task SensitiveHeaders_AreRejectedBeforeSending(string header)
    {
        var requests = 0;
        using var http = new HttpClient(new Handler((_, _) => { requests++; throw new InvalidOperationException(); }));
        using var transport = new RemoteDriveTransport(http);
        var download = new RemoteDriveDownload { Url = Url };
        download.Headers[header] = "private-token";
        var error = await Assert.ThrowsAsync<FileStorageException>(() => transport.ReadAsync(download, (_, _) => Task.CompletedTask, default));
        Assert.Equal(FileStorageError.Configuration, error.Error);
        Assert.Equal(0, requests);
    }

    /// <summary>成功响应后的应用异常保持原实例，同时释放响应资源。</summary>
    [Fact]
    public async Task ConsumerFailure_IsPreservedAndResponseDisposed()
    {
        using var body = new MemoryStream(new byte[] { 1 });
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) })));
        using var transport = new RemoteDriveTransport(http);
        var expected = new InvalidOperationException("consumer failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadAsync(new() { Url = Url },
            (_, _) => Task.FromException(expected), default));
        Assert.Same(expected, actual);
        Assert.False(body.CanRead);
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => _send(request, ct);
    }
}
