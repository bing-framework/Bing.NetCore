using System.Net;

namespace Bing.Http.Resilience;

/// <summary>单次发送借用原请求内容；原请求继续拥有内容。不会用于并行或重试请求。</summary>
internal sealed class BorrowedRequestContent : HttpContent
{
    private readonly HttpContent _inner;
    public BorrowedRequestContent(HttpContent inner)
    {
        _inner = inner;
        foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
    protected override bool TryComputeLength(out long length)
    { length = _inner.Headers.ContentLength.GetValueOrDefault(); return _inner.Headers.ContentLength.HasValue; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _inner.CopyToAsync(stream, context);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        _inner.CopyToAsync(stream, context, cancellationToken);
}
