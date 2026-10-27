using System.Net;
using System.Text;
using System.Text.Json;
using Bing.Application.Services;
using Bing.Content;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Contracts;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public interface IProbe : IAppService
{
    Task<string> GetAsync(CancellationToken cancellationToken = default);
    Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken = default);
    Task<string> WriteAsync(CancellationToken cancellationToken = default);
}

internal static class TestSupport
{
    internal static bool IsDefinition(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/definition", StringComparison.Ordinal);

    internal static HttpResponseMessage Json<T>(T value, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    internal static HttpResponseMessage Definition() => Json(new BingDynamicApiDefinition
    {
        ApiVersion = "v1",
        Services =
        {
            new BingDynamicApiServiceDefinition
            {
                ServiceTypeName = typeof(IProbe).FullName,
                ServiceName = "probe",
                Methods =
                {
                    Method(nameof(IProbe.GetAsync), "GET", false),
                    Method(nameof(IProbe.DownloadAsync), "GET", true),
                    Method(nameof(IProbe.WriteAsync), "POST", false)
                }
            }
        }
    });

    private static BingDynamicApiMethodDefinition Method(string name, string verb, bool stream) => new()
    {
        MethodName = name, HttpMethod = verb, RouteTemplate = "api/probe/" + name,
        ReturnTypeName = stream ? typeof(IRemoteStreamContent).FullName : typeof(string).FullName,
        IsStreamResult = stream,
        Parameters =
        {
            new BingDynamicApiParameterDefinition
            {
                Name = "cancellationToken", Position = 0, TypeName = typeof(CancellationToken).FullName,
                BindingSource = "Special", IsCancellationToken = true
            }
        }
    };
}

internal sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        callback(request, cancellationToken);
}

internal sealed class BlockingStream : Stream
{
    internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Disposed { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Wait(cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Wait(cancellationToken));
    private async Task<int> Wait(CancellationToken token)
    {
        Started.TrySetResult(true);
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return 0;
    }
    protected override void Dispose(bool disposing) { Disposed |= disposing; base.Dispose(disposing); }
}
